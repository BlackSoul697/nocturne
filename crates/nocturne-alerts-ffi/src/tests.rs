//! FFI-boundary tests: drive the C ABI functions directly (pointers in,
//! pointers out) — the full golden corpus threaded through the evaluate
//! envelope, plus the error-envelope contract (null, invalid UTF-8, malformed
//! JSON, bad schema, panic path).

use std::ffi::{CStr, CString, c_char};
use std::fs;
use std::path::PathBuf;

use serde_json::{Map, Value, json};

use crate::{
    boundary, nocturne_alerts_classify, nocturne_alerts_evaluate, nocturne_alerts_evaluate_node,
    nocturne_alerts_free_string, nocturne_alerts_leaf_paths, nocturne_alerts_version,
};

/// Calls an FFI function with `input`, copies the result into a Rust string
/// and frees the native allocation.
fn call(f: unsafe extern "C" fn(*const c_char) -> *mut c_char, input: &str) -> String {
    let c_input = CString::new(input).expect("test input has no NUL");
    unsafe {
        let ptr = f(c_input.as_ptr());
        assert!(!ptr.is_null(), "FFI returned null pointer");
        let out = CStr::from_ptr(ptr)
            .to_str()
            .expect("valid UTF-8")
            .to_string();
        nocturne_alerts_free_string(ptr);
        out
    }
}

fn call_json(f: unsafe extern "C" fn(*const c_char) -> *mut c_char, input: &str) -> Value {
    serde_json::from_str(&call(f, input)).expect("FFI returned valid JSON")
}

fn evaluate(request: &Value) -> Value {
    call_json(nocturne_alerts_evaluate, &request.to_string())
}

// ---------------------------------------------------------------------------
// Version + free
// ---------------------------------------------------------------------------

#[test]
fn version_returns_crate_version() {
    unsafe {
        let ptr = nocturne_alerts_version();
        assert!(!ptr.is_null());
        let version = CStr::from_ptr(ptr).to_str().unwrap().to_string();
        nocturne_alerts_free_string(ptr);
        assert_eq!(version, env!("CARGO_PKG_VERSION"));
    }
}

#[test]
fn free_string_accepts_null() {
    unsafe { nocturne_alerts_free_string(std::ptr::null_mut()) };
}

// ---------------------------------------------------------------------------
// Corpus round-trip through the C ABI
// ---------------------------------------------------------------------------

#[derive(serde::Deserialize)]
struct ScenarioFile {
    name: String,
    rules: Vec<Value>,
    ticks: Vec<ScenarioTick>,
}

#[derive(serde::Deserialize)]
struct ScenarioTick {
    at: String,
    context: Value,
}

fn corpus_dir() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../../tests/Parity/AlertEngineCorpus")
        .canonicalize()
        .expect("corpus directory exists")
}

/// Lists every corpus scenario file (excluding the `.expected.json`
/// snapshots), sorted for determinism.
fn scenario_paths() -> Vec<PathBuf> {
    let mut paths: Vec<PathBuf> = fs::read_dir(corpus_dir())
        .expect("read corpus dir")
        .map(|e| e.expect("dir entry").path())
        .filter(|p| {
            p.extension().is_some_and(|ext| ext == "json")
                && !p
                    .file_name()
                    .is_some_and(|n| n.to_string_lossy().ends_with(".expected.json"))
        })
        .collect();
    paths.sort();
    paths
}

fn load_scenario(path: &PathBuf) -> (ScenarioFile, Value) {
    let scenario: ScenarioFile =
        serde_json::from_str(&fs::read_to_string(path).expect("read scenario"))
            .unwrap_or_else(|e| panic!("parse {}: {e}", path.display()));
    let expected_path = path.with_file_name(format!(
        "{}.expected.json",
        path.file_stem().unwrap().to_string_lossy()
    ));
    let expected: Value =
        serde_json::from_str(&fs::read_to_string(&expected_path).expect("read expected"))
            .expect("parse expected");
    (scenario, expected)
}

/// Drives one scenario through an evaluate-envelope function (C ABI or
/// UniFFI), threading the timers/tracker state envelopes between ticks exactly
/// as a host would, and returns the assembled expected-file Value.
fn run_scenario(scenario: &ScenarioFile, evaluate: impl Fn(&Value) -> Value) -> Value {
    // Per-rule persisted state, plus the shared next-excursion ordinal.
    let mut timers: Map<String, Value> = Map::new(); // rule id -> timers object
    let mut trackers: Map<String, Value> = Map::new(); // rule id -> tracker object
    let mut next_ordinal: u64 = 1;

    let ticks: Vec<Value> = scenario
        .ticks
        .iter()
        .map(|tick| {
            let rules: Vec<Value> = scenario
                .rules
                .iter()
                .map(|rule| {
                    let rule_id = rule["id"].as_str().expect("rule id").to_string();
                    let mut tracker = trackers
                        .get(&rule_id)
                        .and_then(|t| t.as_object().cloned())
                        .unwrap_or_default();
                    tracker.insert("next_excursion_ordinal".into(), json!(next_ordinal));

                    let request = json!({
                        "schema_version": 1,
                        "rule": rule,
                        "context": tick.context,
                        "now": tick.at,
                        "timers": timers.get(&rule_id).cloned().unwrap_or_else(|| json!({})),
                        "tracker": tracker,
                    });

                    let response = evaluate(&request);
                    assert_eq!(
                        response["ok"],
                        Value::Bool(true),
                        "scenario {} tick {} rule {}: {}",
                        scenario.name,
                        tick.at,
                        rule_id,
                        response["error"]
                    );

                    timers.insert(rule_id.clone(), response["timers"].clone());
                    trackers.insert(rule_id.clone(), response["tracker"].clone());
                    next_ordinal = response["tracker"]["next_excursion_ordinal"]
                        .as_u64()
                        .expect("next_excursion_ordinal present");

                    response["result"].clone()
                })
                .collect();
            json!({ "at": tick.at, "rules": rules })
        })
        .collect();

    json!({
        "schema_version": 1,
        "scenario": scenario.name,
        "ticks": ticks,
    })
}

/// Runs the full corpus through an evaluate-envelope function and pins every
/// scenario against its committed `.expected.json` snapshot.
fn assert_corpus_round_trips(evaluate: impl Fn(&Value) -> Value) {
    let scenario_paths = scenario_paths();
    assert!(
        scenario_paths.len() >= 100,
        "expected >= 100 corpus scenarios, found {}",
        scenario_paths.len()
    );

    let mut failed: Vec<String> = Vec::new();
    for path in &scenario_paths {
        let (scenario, expected) = load_scenario(path);
        let actual = run_scenario(&scenario, &evaluate);
        if actual != expected {
            failed.push(format!(
                "scenario {}: FFI output differs from expected snapshot",
                scenario.name
            ));
        }
    }
    assert!(
        failed.is_empty(),
        "{} of {} scenarios diverged:\n{}",
        failed.len(),
        scenario_paths.len(),
        failed.join("\n")
    );
}

#[test]
fn corpus_round_trips_through_c_abi() {
    assert_corpus_round_trips(evaluate);
}

#[test]
fn state_threading_survives_serialisation() {
    // A sustained rule needs its timer back on the next tick; a tracker in
    // hysteresis needs updated_at. Exercise both through two manual calls.
    let rule = json!({
        "id": "00000000-0000-0000-0000-00000000abcd",
        "condition_type": "sustained",
        "condition_params": {
            "minutes": 10,
            "child": { "type": "threshold", "threshold": { "direction": "below", "value": 70 } }
        }
    });
    let context = json!({ "latest_value": 60, "latest_timestamp": "2026-01-05T12:00:00Z" });

    let first = evaluate(&json!({
        "schema_version": 1,
        "rule": rule,
        "context": context,
        "now": "2026-01-05T12:00:00Z",
    }));
    assert_eq!(first["ok"], Value::Bool(true));
    assert_eq!(first["result"]["root"], Value::Bool(false));
    assert_eq!(first["timers"]["sustained"], json!("2026-01-05T12:00:00Z"));
    assert_eq!(first["tracker"]["state"], json!("idle"));
    assert_eq!(first["tracker"]["next_excursion_ordinal"], json!(1));

    let second = evaluate(&json!({
        "schema_version": 1,
        "rule": rule,
        "context": context,
        "now": "2026-01-05T12:10:00Z",
        "timers": first["timers"],
        "tracker": first["tracker"],
    }));
    assert_eq!(second["ok"], Value::Bool(true));
    assert_eq!(second["result"]["root"], Value::Bool(true));
    assert_eq!(second["result"]["transition"], json!("opened"));
    assert_eq!(second["result"]["tracker"]["excursion"], json!(1));
    assert_eq!(second["tracker"]["state"], json!("active"));
    assert_eq!(second["tracker"]["active_excursion_ordinal"], json!(1));
    assert_eq!(second["tracker"]["next_excursion_ordinal"], json!(2));
    assert_eq!(
        second["tracker"]["updated_at"],
        json!("2026-01-05T12:10:00Z")
    );
}

// ---------------------------------------------------------------------------
// Evaluate node (auxiliary scopes: snooze conditions, sweep auto-resolve)
// ---------------------------------------------------------------------------

fn evaluate_node(request: &Value) -> Value {
    call_json(nocturne_alerts_evaluate_node, &request.to_string())
}

#[test]
fn evaluate_node_evaluates_a_tree_with_a_root_override() {
    let request = json!({
        "schema_version": 1,
        "rule_id": "00000000-0000-0000-0000-000000000001",
        "node": {
            "type": "composite",
            "composite": {
                "operator": "and",
                "conditions": [
                    { "type": "threshold", "threshold": { "direction": "below", "value": 70 } },
                    { "type": "iob", "iob": { "operator": ">", "value": 1 } }
                ]
            }
        },
        "root": "snooze",
        "context": { "latest_value": 60, "latest_timestamp": "2026-01-05T12:00:00Z", "iob_units": 2 },
        "now": "2026-01-05T12:00:00Z",
    });
    let response = evaluate_node(&request);
    assert_eq!(response["ok"], Value::Bool(true));
    assert_eq!(response["value"], Value::Bool(true));
    assert_eq!(response["timers"], json!({}));
    assert_eq!(response["timer_ops"], json!([]));
}

#[test]
fn evaluate_node_threads_sustained_timers_under_the_root_override() {
    let rule_id = "00000000-0000-0000-0000-000000000002";
    let node = json!({
        "type": "sustained",
        "sustained": {
            "minutes": 10,
            "child": { "type": "threshold", "threshold": { "direction": "above", "value": 180 } }
        }
    });
    let context = json!({ "latest_value": 200, "latest_timestamp": "2026-01-05T12:00:00Z" });

    // First true sets the timer under the overridden root path and returns false.
    let first = evaluate_node(&json!({
        "schema_version": 1,
        "rule_id": rule_id,
        "node": node,
        "root": "auto_resolve",
        "context": context,
        "now": "2026-01-05T12:00:00Z",
    }));
    assert_eq!(first["ok"], Value::Bool(true));
    assert_eq!(first["value"], Value::Bool(false));
    assert_eq!(
        first["timers"]["auto_resolve"],
        json!("2026-01-05T12:00:00Z")
    );
    assert_eq!(
        first["timer_ops"],
        json!([{ "op": "set", "path": "auto_resolve", "at": "2026-01-05T12:00:00Z" }])
    );

    // Threading the persisted timer back in completes the window on schedule.
    let second = evaluate_node(&json!({
        "schema_version": 1,
        "rule_id": rule_id,
        "node": node,
        "root": "auto_resolve",
        "context": context,
        "now": "2026-01-05T12:10:00Z",
        "timers": first["timers"],
    }));
    assert_eq!(second["ok"], Value::Bool(true));
    assert_eq!(second["value"], Value::Bool(true));
    assert_eq!(second["timer_ops"], json!([]));
}

#[test]
fn evaluate_node_defaults_root_to_the_verbatim_type() {
    let request = json!({
        "schema_version": 1,
        "rule_id": "00000000-0000-0000-0000-000000000003",
        "node": {
            "type": "sustained",
            "sustained": {
                "minutes": 5,
                "child": { "type": "threshold", "threshold": { "direction": "below", "value": 70 } }
            }
        },
        "context": { "latest_value": 60, "latest_timestamp": "2026-01-05T12:00:00Z" },
        "now": "2026-01-05T12:00:00Z",
    });
    let response = evaluate_node(&request);
    assert_eq!(response["ok"], Value::Bool(true));
    assert_eq!(
        response["timers"]["sustained"],
        json!("2026-01-05T12:00:00Z")
    );
}

#[test]
fn evaluate_node_unknown_kind_is_silent_false() {
    let request = json!({
        "schema_version": 1,
        "rule_id": "00000000-0000-0000-0000-000000000004",
        "node": { "type": "nope" },
        "root": "snooze",
        "context": {},
        "now": "2026-01-05T12:00:00Z",
    });
    let response = evaluate_node(&request);
    assert_eq!(response["ok"], Value::Bool(true));
    assert_eq!(response["value"], Value::Bool(false));
}

#[test]
fn evaluate_node_rejects_malformed_node() {
    let request = json!({
        "schema_version": 1,
        "rule_id": "00000000-0000-0000-0000-000000000005",
        "node": "not an object",
        "context": {},
        "now": "2026-01-05T12:00:00Z",
    });
    assert_error(&evaluate_node(&request), "malformed condition node");
}

#[test]
fn evaluate_node_rejects_null_pointer() {
    let response: Value = unsafe {
        let ptr = nocturne_alerts_evaluate_node(std::ptr::null());
        let out = CStr::from_ptr(ptr).to_str().unwrap().to_string();
        nocturne_alerts_free_string(ptr);
        serde_json::from_str(&out).unwrap()
    };
    assert_error(&response, "null");
}

// ---------------------------------------------------------------------------
// Error envelopes
// ---------------------------------------------------------------------------

fn assert_error(response: &Value, fragment: &str) {
    assert_eq!(response["schema_version"], json!(1));
    assert_eq!(response["ok"], Value::Bool(false));
    let error = response["error"].as_str().expect("error message present");
    assert!(
        error.contains(fragment),
        "expected error containing '{fragment}', got '{error}'"
    );
}

#[test]
fn evaluate_rejects_null_pointer() {
    let response: Value = unsafe {
        let ptr = nocturne_alerts_evaluate(std::ptr::null());
        let out = CStr::from_ptr(ptr).to_str().unwrap().to_string();
        nocturne_alerts_free_string(ptr);
        serde_json::from_str(&out).unwrap()
    };
    assert_error(&response, "null");
}

#[test]
fn leaf_paths_rejects_null_pointer() {
    let response: Value = unsafe {
        let ptr = nocturne_alerts_leaf_paths(std::ptr::null());
        let out = CStr::from_ptr(ptr).to_str().unwrap().to_string();
        nocturne_alerts_free_string(ptr);
        serde_json::from_str(&out).unwrap()
    };
    assert_error(&response, "null");
}

#[test]
fn evaluate_rejects_invalid_utf8() {
    // 0xFF can never appear in UTF-8.
    let bytes = CString::new(vec![0xFFu8, 0x7B, 0x7D]).unwrap();
    let response: Value = unsafe {
        let ptr = nocturne_alerts_evaluate(bytes.as_ptr());
        let out = CStr::from_ptr(ptr).to_str().unwrap().to_string();
        nocturne_alerts_free_string(ptr);
        serde_json::from_str(&out).unwrap()
    };
    assert_error(&response, "not valid UTF-8");
}

#[test]
fn evaluate_rejects_malformed_json() {
    assert_error(
        &call_json(nocturne_alerts_evaluate, "{ this is not json"),
        "invalid request envelope",
    );
}

#[test]
fn evaluate_rejects_wrong_schema_version() {
    let request = json!({
        "schema_version": 2,
        "rule": { "id": "00000000-0000-0000-0000-000000000001", "condition_type": "threshold" },
        "context": {},
        "now": "2026-01-05T12:00:00Z",
    });
    assert_error(&evaluate(&request), "unsupported schema_version 2");
}

#[test]
fn evaluate_rejects_unknown_condition_type() {
    let request = json!({
        "schema_version": 1,
        "rule": { "id": "00000000-0000-0000-0000-000000000001", "condition_type": "nope" },
        "context": {},
        "now": "2026-01-05T12:00:00Z",
    });
    assert_error(&evaluate(&request), "unknown condition_type 'nope'");
}

#[test]
fn evaluate_rejects_tracker_state_without_updated_at() {
    let request = json!({
        "schema_version": 1,
        "rule": {
            "id": "00000000-0000-0000-0000-000000000001",
            "condition_type": "threshold",
            "condition_params": { "direction": "below", "value": 70 }
        },
        "context": {},
        "now": "2026-01-05T12:00:00Z",
        "tracker": { "state": "active", "confirmation_count": 0, "next_excursion_ordinal": 2 },
    });
    assert_error(&evaluate(&request), "tracker.updated_at is required");
}

#[test]
fn boundary_converts_panics_to_error_envelopes() {
    let ptr = boundary(|| panic!("deliberate test panic"));
    let response: Value = unsafe {
        let out = CStr::from_ptr(ptr).to_str().unwrap().to_string();
        nocturne_alerts_free_string(ptr);
        serde_json::from_str(&out).unwrap()
    };
    assert_error(&response, "panic in alert engine: deliberate test panic");
}

// ---------------------------------------------------------------------------
// Leaf paths
// ---------------------------------------------------------------------------

#[test]
fn leaf_paths_enumerates_nodes_and_leaves() {
    let node = json!({
        "type": "composite",
        "composite": {
            "operator": "and",
            "conditions": [
                {
                    "type": "sustained",
                    "sustained": {
                        "minutes": 10,
                        "child": { "type": "threshold", "threshold": { "direction": "below", "value": 70 } }
                    }
                },
                { "type": "iob", "iob": { "operator": ">", "value": 1 } }
            ]
        }
    });
    let response = call_json(nocturne_alerts_leaf_paths, &node.to_string());
    assert_eq!(response["ok"], Value::Bool(true));
    assert_eq!(response["root"], json!("composite"));
    assert_eq!(
        response["paths"],
        json!([
            "composite",
            "composite[0].sustained",
            "composite[0].sustained[0].threshold",
            "composite[1].iob",
        ])
    );
    assert_eq!(
        response["leaves"],
        json!([
            { "leaf_id": 0, "path": "composite[0].sustained[0].threshold" },
            { "leaf_id": 1, "path": "composite[1].iob" },
        ])
    );
}

#[test]
fn leaf_paths_honours_root_override() {
    let input = json!({
        "root": "auto_resolve",
        "node": {
            "type": "sustained",
            "sustained": {
                "minutes": 5,
                "child": { "type": "threshold", "threshold": { "direction": "above", "value": 180 } }
            }
        }
    });
    let response = call_json(nocturne_alerts_leaf_paths, &input.to_string());
    assert_eq!(response["ok"], Value::Bool(true));
    assert_eq!(response["root"], json!("auto_resolve"));
    assert_eq!(
        response["paths"],
        json!(["auto_resolve", "auto_resolve[0].threshold"])
    );
    assert_eq!(
        response["leaves"],
        json!([{ "leaf_id": 0, "path": "auto_resolve[0].threshold" }])
    );
}

#[test]
fn leaf_paths_treats_container_with_missing_child_as_leaf() {
    // A sustained node without a child fails the container guard and IS a
    // leaf (LeafIdentity anomaly — normative).
    let node = json!({ "type": "sustained", "sustained": { "minutes": 10 } });
    let response = call_json(nocturne_alerts_leaf_paths, &node.to_string());
    assert_eq!(response["ok"], Value::Bool(true));
    assert_eq!(response["paths"], json!(["sustained"]));
    assert_eq!(
        response["leaves"],
        json!([{ "leaf_id": 0, "path": "sustained" }])
    );
}

#[test]
fn leaf_paths_rejects_non_object() {
    assert_error(
        &call_json(nocturne_alerts_leaf_paths, "[1, 2, 3]"),
        "must be a JSON object",
    );
}

#[test]
fn leaf_paths_rejects_malformed_node() {
    // `type` must be a string (or null/absent) — a number is a JsonException
    // in the C# engine.
    assert_error(
        &call_json(nocturne_alerts_leaf_paths, r#"{ "type": 5 }"#),
        "malformed condition node",
    );
}

// ---------------------------------------------------------------------------
// Classify (scoped Do Not Disturb scope class — ADR 0004)
// ---------------------------------------------------------------------------

fn classify(request: &Value) -> Value {
    call_json(nocturne_alerts_classify, &request.to_string())
}

fn classify_scope(condition_type: &str, condition_params: Value) -> String {
    let response = classify(&json!({
        "schema_version": 1,
        "condition_type": condition_type,
        "condition_params": condition_params,
    }));
    assert_eq!(response["schema_version"], json!(1));
    assert_eq!(response["ok"], Value::Bool(true), "error: {}", response["error"]);
    response["scope_class"]
        .as_str()
        .expect("scope_class present")
        .to_string()
}

#[test]
fn classify_threshold_below_is_low() {
    assert_eq!(classify_scope("threshold", json!({ "direction": "below", "value": 70 })), "low");
}

#[test]
fn classify_threshold_above_is_high() {
    assert_eq!(classify_scope("threshold", json!({ "direction": "above", "value": 250 })), "high");
}

#[test]
fn classify_composite_mixed_directions_is_composite() {
    // Children are full nodes (carry their own `type` + payload), exactly the
    // stored composite shape.
    let params = json!({
        "operator": "or",
        "conditions": [
            { "type": "threshold", "threshold": { "direction": "below", "value": 70 } },
            { "type": "threshold", "threshold": { "direction": "above", "value": 250 } }
        ]
    });
    assert_eq!(classify_scope("composite", params), "composite");
}

#[test]
fn classify_signal_loss_is_undirected() {
    assert_eq!(classify_scope("signal_loss", json!({ "timeout_minutes": 20 })), "undirected");
}

#[test]
fn classify_silent_fails_unknown_type_to_undirected() {
    // Unlike evaluate, an unknown condition_type is NOT an envelope error — the
    // crate's classify is the all-only safe default.
    assert_eq!(classify_scope("teleport", json!({})), "undirected");
}

#[test]
fn classify_defaults_missing_params_to_undirected() {
    let response = classify(&json!({
        "schema_version": 1,
        "condition_type": "threshold",
    }));
    assert_eq!(response["ok"], Value::Bool(true));
    assert_eq!(response["scope_class"], json!("undirected"));
}

#[test]
fn classify_rejects_null_pointer() {
    let response: Value = unsafe {
        let ptr = nocturne_alerts_classify(std::ptr::null());
        let out = CStr::from_ptr(ptr).to_str().unwrap().to_string();
        nocturne_alerts_free_string(ptr);
        serde_json::from_str(&out).unwrap()
    };
    assert_error(&response, "null");
}

#[test]
fn classify_rejects_malformed_json() {
    assert_error(
        &call_json(nocturne_alerts_classify, "{ this is not json"),
        "invalid request envelope",
    );
}

#[test]
fn classify_rejects_wrong_schema_version() {
    let request = json!({
        "schema_version": 2,
        "condition_type": "threshold",
        "condition_params": { "direction": "below", "value": 70 },
    });
    assert_error(&classify(&request), "unsupported schema_version 2");
}

// ---------------------------------------------------------------------------
// UniFFI surface (feature-gated): the Kotlin-facing functions must expose the
// exact same envelope contract as the C ABI.
// ---------------------------------------------------------------------------

#[cfg(feature = "uniffi")]
mod uniffi_surface {
    use super::{assert_error, load_scenario, run_scenario, scenario_paths};
    use crate::uniffi_api;
    use serde_json::{Value, json};

    fn evaluate(request: &Value) -> Value {
        serde_json::from_str(&uniffi_api::evaluate(request.to_string()))
            .expect("uniffi evaluate returned valid JSON")
    }

    #[test]
    fn version_matches_crate_version() {
        assert_eq!(uniffi_api::version(), env!("CARGO_PKG_VERSION"));
    }

    #[test]
    fn corpus_scenario_round_trips_through_uniffi_surface() {
        // One full scenario threaded tick-by-tick through the uniffi-exported
        // `evaluate`, pinned against the committed snapshot — same driver as
        // the C ABI corpus test. (The full corpus already runs through the C
        // ABI in this build; both paths share one envelope implementation.)
        let paths = scenario_paths();
        let (scenario, expected) = load_scenario(paths.first().expect("corpus is non-empty"));
        let actual = run_scenario(&scenario, evaluate);
        assert_eq!(
            actual, expected,
            "scenario {}: uniffi output differs from expected snapshot",
            scenario.name
        );
    }

    #[test]
    fn evaluate_node_shares_the_envelope_contract() {
        let response: Value = serde_json::from_str(&uniffi_api::evaluate_node(
            json!({
                "schema_version": 1,
                "rule_id": "00000000-0000-0000-0000-000000000001",
                "node": { "type": "threshold", "threshold": { "direction": "below", "value": 70 } },
                "root": "snooze",
                "context": { "latest_value": 60, "latest_timestamp": "2026-01-05T12:00:00Z" },
                "now": "2026-01-05T12:00:00Z",
            })
            .to_string(),
        ))
        .expect("valid JSON");
        assert_eq!(response["ok"], Value::Bool(true));
        assert_eq!(response["value"], Value::Bool(true));
    }

    #[test]
    fn leaf_paths_shares_the_envelope_contract() {
        let response: Value = serde_json::from_str(&uniffi_api::leaf_paths(
            json!({
                "root": "auto_resolve",
                "node": { "type": "threshold", "threshold": { "direction": "above", "value": 180 } }
            })
            .to_string(),
        ))
        .expect("valid JSON");
        assert_eq!(response["ok"], Value::Bool(true));
        assert_eq!(
            response["leaves"],
            json!([{ "leaf_id": 0, "path": "auto_resolve" }])
        );
    }

    #[test]
    fn errors_come_back_as_envelopes_not_exceptions() {
        let malformed: Value =
            serde_json::from_str(&uniffi_api::evaluate("{ nope".to_string())).expect("valid JSON");
        assert_error(&malformed, "invalid request envelope");

        let bad_schema = evaluate(&json!({
            "schema_version": 2,
            "rule": { "id": "00000000-0000-0000-0000-000000000001", "condition_type": "threshold" },
            "context": {},
            "now": "2026-01-05T12:00:00Z",
        }));
        assert_error(&bad_schema, "unsupported schema_version 2");
    }
}
