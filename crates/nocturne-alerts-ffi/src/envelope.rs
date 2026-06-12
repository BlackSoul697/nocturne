//! Request/response envelope: serde wire types, the per-call evaluation
//! driver, and the leaf/path enumerator. The JSON shapes reuse the golden
//! corpus interchange format (`ScenarioRule` / `ScenarioContext` /
//! `ExpectedRuleResult`) verbatim wherever one exists; the state-carrying
//! `timers` / `tracker` objects are defined here and documented in the crate
//! `README.md`.

use std::collections::BTreeMap;

use chrono::{DateTime, SecondsFormat, Utc};
use serde::Deserialize;
use serde_json::{Map, Value, json};
use uuid::Uuid;

use nocturne_alerts_core::context::SensorContext;
use nocturne_alerts_core::engine::{EngineState, Rule, RuleOutcome, evaluate_rule};
use nocturne_alerts_core::eval::{Env, eval_node};
use nocturne_alerts_core::excursion::{
    CloseReason, TrackerState, TrackerStateKind, TransitionType,
};
use nocturne_alerts_core::model::{ConditionKind, Node, Payload};
use nocturne_alerts_core::paths::child_path;
use nocturne_alerts_core::sustained::{TimerOp, TimerOpKind, TimerStore};

pub const SCHEMA_VERSION: i64 = 1;

// ---------------------------------------------------------------------------
// Request wire types
// ---------------------------------------------------------------------------

#[derive(Deserialize)]
struct EvaluateRequest {
    schema_version: i64,
    rule: WireRule,
    context: SensorContext,
    now: DateTime<Utc>,
    /// Persisted sustained-timer state for this rule: `path -> first_true`.
    #[serde(default)]
    timers: BTreeMap<String, DateTime<Utc>>,
    /// Persisted tracker state. Absent/null means "never evaluated".
    #[serde(default)]
    tracker: Option<WireTracker>,
}

fn default_confirmation_readings() -> i32 {
    1
}

/// `ScenarioRule` corpus shape (unknown fields such as `name` are ignored).
#[derive(Deserialize)]
struct WireRule {
    id: Uuid,
    condition_type: String,
    #[serde(default)]
    condition_params: Value,
    #[serde(default = "default_confirmation_readings")]
    confirmation_readings: i32,
    #[serde(default)]
    hysteresis_minutes: i32,
    #[serde(default)]
    auto_resolve_enabled: bool,
    #[serde(default)]
    auto_resolve_params: Option<Value>,
}

fn default_next_ordinal() -> u32 {
    1
}

#[derive(Deserialize)]
struct WireTracker {
    /// `idle | confirming | active | hysteresis`; absent/null means no
    /// per-rule state exists yet (only the shared ordinal counter is carried).
    #[serde(default)]
    state: Option<String>,
    #[serde(default)]
    confirmation_count: i32,
    #[serde(default)]
    active_excursion_ordinal: Option<u32>,
    /// Required whenever `state` is present (drives hysteresis expiry).
    #[serde(default)]
    updated_at: Option<DateTime<Utc>>,
    /// 1-based ordinal the next opened excursion will receive. Shared across
    /// all rules of a tenant/scenario; thread it between calls.
    #[serde(default = "default_next_ordinal")]
    next_excursion_ordinal: u32,
}

// ---------------------------------------------------------------------------
// Evaluate
// ---------------------------------------------------------------------------

pub fn evaluate(request_json: &str) -> Result<Value, String> {
    let req: EvaluateRequest =
        serde_json::from_str(request_json).map_err(|e| format!("invalid request envelope: {e}"))?;
    if req.schema_version != SCHEMA_VERSION {
        return Err(format!(
            "unsupported schema_version {} (expected {SCHEMA_VERSION})",
            req.schema_version
        ));
    }

    let kind = ConditionKind::from_wire(&req.rule.condition_type).ok_or_else(|| {
        format!(
            "unknown condition_type '{}'",
            req.rule.condition_type.escape_default()
        )
    })?;

    let rule = Rule {
        id: req.rule.id,
        condition_type: kind,
        condition_params: req.rule.condition_params,
        confirmation_readings: req.rule.confirmation_readings,
        hysteresis_minutes: req.rule.hysteresis_minutes,
        auto_resolve_enabled: req.rule.auto_resolve_enabled,
        auto_resolve_params: req.rule.auto_resolve_params,
    };

    let mut state = EngineState::new();
    for (path, at) in &req.timers {
        state.timers.seed(rule.id, path, *at);
    }
    if let Some(w) = &req.tracker {
        state
            .tracker
            .set_next_excursion_ordinal(w.next_excursion_ordinal);
        if let Some(s) = &w.state {
            let state_kind = TrackerStateKind::from_wire(s)
                .ok_or_else(|| format!("unknown tracker state '{}'", s.escape_default()))?;
            let updated_at = w
                .updated_at
                .ok_or("tracker.updated_at is required when tracker.state is present")?;
            state.tracker.restore_state(
                rule.id,
                TrackerState {
                    state: state_kind,
                    confirmation_count: w.confirmation_count,
                    active_excursion: w.active_excursion_ordinal,
                    updated_at,
                },
            );
        }
    }

    let outcome = evaluate_rule(&rule, &req.context, req.now, &mut state);

    Ok(json!({
        "schema_version": SCHEMA_VERSION,
        "ok": true,
        "result": outcome_json(&outcome),
        "timers": timers_json(&state, rule.id),
        "tracker": tracker_state_json(&state, rule.id),
    }))
}

/// RFC 3339 UTC; whole seconds render without a fraction (matching the corpus
/// `yyyy-MM-ddTHH:mm:ssZ` form), sub-second instants keep their precision.
fn fmt_at(at: DateTime<Utc>) -> String {
    at.to_rfc3339_opts(SecondsFormat::AutoSi, true)
}

fn timer_op_json(op: &TimerOp) -> Value {
    let mut o = Map::new();
    o.insert(
        "op".into(),
        Value::String(
            match op.kind {
                TimerOpKind::Set => "set",
                TimerOpKind::Clear => "clear",
            }
            .into(),
        ),
    );
    o.insert("path".into(), Value::String(op.path.clone()));
    if let Some(at) = op.at {
        o.insert("at".into(), Value::String(fmt_at(at)));
    }
    Value::Object(o)
}

/// `ExpectedRuleResult` corpus shape (mirrors the parity harness exactly).
fn outcome_json(outcome: &RuleOutcome) -> Value {
    let mut o = Map::new();
    o.insert("rule_id".into(), Value::String(outcome.rule_id.to_string()));
    if outcome.skipped {
        o.insert("skipped".into(), Value::Bool(true));
        return Value::Object(o);
    }
    o.insert("root".into(), Value::Bool(outcome.root.expect("root set")));
    o.insert(
        "leaves".into(),
        Value::Array(
            outcome
                .leaves
                .iter()
                .map(|(leaf_id, value)| json!({ "leaf_id": leaf_id, "value": value }))
                .collect(),
        ),
    );
    let transition = outcome.transition.expect("transition set");
    o.insert(
        "transition".into(),
        Value::String(
            match transition.kind {
                TransitionType::None => "none",
                TransitionType::ExcursionOpened => "opened",
                TransitionType::ExcursionContinues => "continues",
                TransitionType::HysteresisStarted => "hysteresis_started",
                TransitionType::HysteresisResumed => "hysteresis_resumed",
                TransitionType::ExcursionClosed => "closed",
            }
            .into(),
        ),
    );
    if let Some(reason) = transition.close_reason {
        o.insert(
            "close_reason".into(),
            Value::String(
                match reason {
                    CloseReason::Hysteresis => "hysteresis",
                    CloseReason::AutoResolve => "auto",
                    CloseReason::Manual => "manual",
                }
                .into(),
            ),
        );
    }
    if let Some(tracker) = &outcome.tracker {
        let mut t = Map::new();
        t.insert("state".into(), Value::String(tracker.state.wire().into()));
        t.insert(
            "confirmation_count".into(),
            Value::Number(tracker.confirmation_count.into()),
        );
        if let Some(excursion) = tracker.excursion {
            t.insert("excursion".into(), Value::Number(excursion.into()));
        }
        o.insert("tracker".into(), Value::Object(t));
    }
    if outcome.auto_resolved {
        o.insert("auto_resolved".into(), Value::Bool(true));
    }
    if !outcome.timer_ops.is_empty() {
        o.insert(
            "timer_ops".into(),
            Value::Array(outcome.timer_ops.iter().map(timer_op_json).collect()),
        );
    }
    Value::Object(o)
}

/// Post-evaluation timer state for the rule: `path -> first_true`.
fn timers_json(state: &EngineState, rule_id: Uuid) -> Value {
    let mut o = Map::new();
    for (path, at) in state.timers.snapshot_for_rule(rule_id) {
        o.insert(path, Value::String(fmt_at(at)));
    }
    Value::Object(o)
}

/// Post-evaluation tracker state. `state`/`confirmation_count`/`updated_at`
/// (and `active_excursion_ordinal` when an excursion is active) are present
/// only once per-rule state exists; `next_excursion_ordinal` is always
/// present and must be threaded into the next call (shared across rules).
fn tracker_state_json(state: &EngineState, rule_id: Uuid) -> Value {
    let mut t = Map::new();
    if let Some(s) = state.tracker.state(rule_id) {
        t.insert("state".into(), Value::String(s.state.wire().into()));
        t.insert(
            "confirmation_count".into(),
            Value::Number(s.confirmation_count.into()),
        );
        if let Some(excursion) = s.active_excursion {
            t.insert(
                "active_excursion_ordinal".into(),
                Value::Number(excursion.into()),
            );
        }
        t.insert("updated_at".into(), Value::String(fmt_at(s.updated_at)));
    }
    t.insert(
        "next_excursion_ordinal".into(),
        Value::Number(state.tracker.next_excursion_ordinal().into()),
    );
    Value::Object(t)
}

// ---------------------------------------------------------------------------
// Evaluate node
// ---------------------------------------------------------------------------

/// Request for `nocturne_alerts_evaluate_node`: a single condition tree
/// evaluated outside the per-rule driver (no tracker, no auto-resolve). Used
/// by hosts for auxiliary evaluation scopes — smart-snooze conditions
/// (`root: "snooze"`) and the sweep's periodic auto-resolve
/// (`root: "auto_resolve"`) — which in C# go through
/// `ConditionEvaluatorRegistry.EvaluateNodeAsync` with a reserved path root.
#[derive(Deserialize)]
struct EvaluateNodeRequest {
    schema_version: i64,
    /// Keys sustained timers, exactly like the rule id in `evaluate`.
    rule_id: Uuid,
    /// A full ConditionNode object (`{"type": …, …}`).
    node: Value,
    /// Root path segment override (e.g. `"snooze"`, `"auto_resolve"`).
    /// Defaults to the node's verbatim `type` string.
    #[serde(default)]
    root: Option<String>,
    context: SensorContext,
    now: DateTime<Utc>,
    /// Persisted sustained-timer state for the rule: `path -> first_true`.
    #[serde(default)]
    timers: BTreeMap<String, DateTime<Utc>>,
}

/// Evaluates one condition node for one instant. Unknown node kinds and
/// missing payloads evaluate `false` (silent-fail parity); a structurally
/// malformed node is an envelope error, mirroring the C# callers which all
/// deserialise the tree (and skip on `JsonException`) before dispatching.
pub fn evaluate_node_envelope(request_json: &str) -> Result<Value, String> {
    let req: EvaluateNodeRequest =
        serde_json::from_str(request_json).map_err(|e| format!("invalid request envelope: {e}"))?;
    if req.schema_version != SCHEMA_VERSION {
        return Err(format!(
            "unsupported schema_version {} (expected {SCHEMA_VERSION})",
            req.schema_version
        ));
    }

    let node = Node::parse(&req.node)
        .map_err(|_| "malformed condition node (JsonException-equivalent)".to_string())?;
    let root = req
        .root
        .unwrap_or_else(|| node.type_str.clone().unwrap_or_default());

    let mut timers = TimerStore::new();
    for (path, at) in &req.timers {
        timers.seed(req.rule_id, path, *at);
    }

    let value = {
        let mut env = Env {
            now: req.now,
            rule_id: req.rule_id,
            ctx: &req.context,
            timers: &mut timers,
        };
        eval_node(Some(&node), &root, &mut env)
    };

    let ops = timers.drain_ops();
    let mut timers_obj = Map::new();
    for (path, at) in timers.snapshot_for_rule(req.rule_id) {
        timers_obj.insert(path, Value::String(fmt_at(at)));
    }

    Ok(json!({
        "schema_version": SCHEMA_VERSION,
        "ok": true,
        "value": value,
        "timers": Value::Object(timers_obj),
        "timer_ops": ops.iter().map(timer_op_json).collect::<Vec<_>>(),
    }))
}

// ---------------------------------------------------------------------------
// Leaf paths
// ---------------------------------------------------------------------------

/// Input: a full ConditionNode object (`{"type": …, …}`), or a wrapper
/// `{"node": {…}, "root": "auto_resolve"}` overriding the root path segment
/// (defaults to the node's verbatim `type` string, mirroring
/// `ConditionPath.Walk`).
pub fn leaf_paths(input_json: &str) -> Result<Value, String> {
    let v: Value = serde_json::from_str(input_json).map_err(|e| format!("invalid JSON: {e}"))?;

    let (node_value, root_override) = match &v {
        Value::Object(o) if o.contains_key("node") => {
            let root = match o.get("root") {
                None | Some(Value::Null) => None,
                Some(Value::String(s)) => Some(s.clone()),
                Some(_) => return Err("'root' must be a string".into()),
            };
            (o.get("node").expect("checked above"), root)
        }
        Value::Object(_) => (&v, None),
        _ => return Err("condition node must be a JSON object".into()),
    };

    let node = Node::parse(node_value)
        .map_err(|_| "malformed condition node (JsonException-equivalent)".to_string())?;
    let root = root_override.unwrap_or_else(|| node.type_str.clone().unwrap_or_default());

    let mut paths = Vec::new();
    let mut leaves: Vec<(i32, String)> = Vec::new();
    walk(Some(&node), root.clone(), &mut paths, &mut leaves);

    Ok(json!({
        "schema_version": SCHEMA_VERSION,
        "ok": true,
        "root": root,
        "paths": paths,
        "leaves": leaves
            .iter()
            .map(|(leaf_id, path)| json!({ "leaf_id": leaf_id, "path": path }))
            .collect::<Vec<_>>(),
    }))
}

/// Pre-order walk emitting every node slot's canonical path; leaf-id
/// assignment mirrors `LeafIdentity.AssignLeafIds` / `collect_leaves`
/// (containers with a missing payload or child ARE leaves; a JSON-null child
/// slot of a composite is a leaf with an empty type segment).
fn walk(
    node: Option<&Node>,
    path: String,
    paths: &mut Vec<String>,
    leaves: &mut Vec<(i32, String)>,
) {
    paths.push(path.clone());
    let Some(node) = node else {
        leaves.push((leaves.len() as i32, path));
        return;
    };
    let lower = node.type_str.as_deref().map(str::to_lowercase);
    match lower.as_deref() {
        Some("composite") => {
            if let Some(Payload::Composite(p)) = node.payload("composite")
                && let Some(children) = &p.conditions
            {
                for (i, child) in children.iter().enumerate() {
                    let cp =
                        child_path(&path, i, child.as_ref().and_then(|c| c.type_str.as_deref()));
                    walk(child.as_ref(), cp, paths, leaves);
                }
                return;
            }
        }
        Some("not") => {
            if let Some(Payload::Not(p)) = node.payload("not")
                && let Some(child) = &p.child
            {
                let cp = child_path(&path, 0, child.type_str.as_deref());
                walk(Some(child), cp, paths, leaves);
                return;
            }
        }
        Some("sustained") => {
            if let Some(Payload::Sustained(p)) = node.payload("sustained")
                && let Some(child) = &p.child
            {
                let cp = child_path(&path, 0, child.type_str.as_deref());
                walk(Some(child), cp, paths, leaves);
                return;
            }
        }
        _ => {}
    }
    leaves.push((leaves.len() as i32, path));
}
