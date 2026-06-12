# nocturne-alerts-ffi

C ABI wrapper around [`nocturne-alerts-core`](../nocturne-alerts-core): the
alert evaluation engine exposed as JSON-in/JSON-out functions for non-Rust
hosts. The .NET bindings live in `src/Core/Nocturne.Core.Alerts.Native/`
(`AlertsInterop` + `RustAlertEngine`); Kotlin bindings (Phase 4) consume the
same contract.

```bash
# from crates/
cargo build --release -p nocturne-alerts-ffi
# -> target/release/nocturne_alerts.dll   (Windows)
# -> target/release/libnocturne_alerts.so (Linux)
```

The crate builds a `cdylib` and a `staticlib`, library name `nocturne_alerts`.

## C ABI

```c
char* nocturne_alerts_version(void);
char* nocturne_alerts_evaluate(const char* request_json);
char* nocturne_alerts_evaluate_node(const char* request_json);
char* nocturne_alerts_leaf_paths(const char* condition_node_json);
void  nocturne_alerts_free_string(char* ptr);
```

- All strings are UTF-8, NUL-terminated. Every returned pointer is owned by
  the caller and must be released with `nocturne_alerts_free_string` exactly
  once (null is a no-op).
- `nocturne_alerts_version` returns a plain version string (e.g. `0.1.0`),
  not JSON. Everything else returns a JSON envelope.
- The library never panics across the boundary and never crashes on bad
  input: panics, null pointers, invalid UTF-8 and malformed JSON all come
  back as the error envelope:

```json
{ "schema_version": 1, "ok": false, "error": "human-readable message" }
```

## Evaluate envelope (`nocturne_alerts_evaluate`)

One call = one rule evaluated for one tick, mirroring the orchestrator
contract (`AlertOrchestrator.EvaluateRuleAsync`: root eval → leaf force-eval
log → excursion tracker → auto-resolve). The engine is stateless between
calls: **all** evaluation state (sustained timers, tracker) is carried in and
out as data, and the host persists it.

The `rule`, `context` and `result` shapes are exactly the golden-corpus
interchange shapes (`ScenarioRule`, `ScenarioContext`, `ExpectedRuleResult` in
`tests/Parity/Nocturne.Alerts.ParityCorpus.Generator/Harness/ScenarioModels.cs`)
— the corpus in `tests/Parity/AlertEngineCorpus/` is the machine-checkable
spec for all of them. Timestamps are RFC 3339 UTC; the engine emits
whole-second instants without a fraction (`2026-01-05T12:00:00Z`) and
preserves sub-second precision when present.

### Request

```jsonc
{
  "schema_version": 1,                      // required; only 1 is accepted
  "rule": {                                 // ScenarioRule corpus shape
    "id": "00000000-0000-0000-0000-000000000001",
    "condition_type": "sustained",          // canonical wire string
    "condition_params": { /* payload */ },  // payload object as stored; null allowed
    "confirmation_readings": 1,             // default 1
    "hysteresis_minutes": 0,                // default 0
    "auto_resolve_enabled": false,          // default false
    "auto_resolve_params": { "type": "…" }  // full ConditionNode or null
  },
  "context": { /* ScenarioContext corpus shape */ },
  "now": "2026-01-05T12:00:00Z",            // the evaluation instant
  "timers": {                               // optional; default {}
    "sustained": "2026-01-05T11:55:00Z"     // condition path -> first-true instant
  },
  "tracker": {                              // optional; absent = never evaluated
    "state": "active",                      // idle|confirming|active|hysteresis; absent = no per-rule state yet
    "confirmation_count": 0,
    "active_excursion_ordinal": 3,          // present only while an excursion is active
    "updated_at": "2026-01-05T11:55:00Z",   // REQUIRED whenever state is present (drives hysteresis expiry)
    "next_excursion_ordinal": 4             // default 1; see "State threading"
  }
}
```

Unknown fields (e.g. the scenario `name`) are ignored, so a corpus
`ScenarioRule` object can be passed as `rule` verbatim.

### Response

```jsonc
{
  "schema_version": 1,
  "ok": true,
  "result": { /* ExpectedRuleResult corpus shape:
                 rule_id, skipped?, root, leaves[], transition, close_reason?,
                 tracker {state, confirmation_count, excursion?},
                 auto_resolved?, timer_ops[] */ },
  "timers": { "sustained": "2026-01-05T12:00:00Z" },  // full post-state; persist verbatim
  "tracker": {                                        // full post-state; persist verbatim
    "state": "active",
    "confirmation_count": 0,
    "active_excursion_ordinal": 3,
    "updated_at": "2026-01-05T12:00:00Z",
    "next_excursion_ordinal": 4
  }
}
```

Failure modes that are **data**, not errors (matching C# engine semantics):
unknown leaf types, malformed payloads inside trees, null condition records —
these evaluate `false` inside `result`. Envelope-level errors (`ok: false`)
are reserved for unusable requests: malformed JSON, wrong `schema_version`,
unknown root `condition_type`, unknown `tracker.state`, or a tracker `state`
without `updated_at`.

A rule whose root type has no evaluator (`signal_loss`) is skipped exactly
like the orchestrator skips it: `result` is `{rule_id, skipped: true}` and the
state passes through unchanged.

### State threading

- **`timers`** are per-rule: key is the condition path of the `sustained`
  node, value the first-true instant. Persist the response `timers` object and
  send it back on the rule's next evaluation. (It is keyed by path only — the
  rule id is implicit in the call.)
- **`tracker`** per-rule fields (`state`, `confirmation_count`,
  `active_excursion_ordinal`, `updated_at`) round-trip the same way and are
  absent until the rule's first non-skipped evaluation.
- **`next_excursion_ordinal`** is the 1-based ordinal the next opened
  excursion will receive. It is **shared across all rules** of a tenant (the
  corpus assigns excursion ordinals in creation order across the whole
  scenario), so thread the latest response value into the next call in
  evaluation order, whichever rule it is for. Hosts that key excursions
  differently can ignore the ordinals entirely and treat
  `result.transition` (`opened`/`closed`) as the event source.

## Evaluate node (`nocturne_alerts_evaluate_node`)

Evaluates a single condition tree for one instant **outside** the per-rule
driver: no tracker, no auto-resolve, no leaf log — just the node's truth plus
sustained-timer state threading. This is the FFI counterpart of
`ConditionEvaluatorRegistry.EvaluateNodeAsync` and exists for the auxiliary
evaluation scopes the backend runs against reserved path roots:
smart-snooze conditions (`root: "snooze"`) and the sweep's periodic
auto-resolve (`root: "auto_resolve"`).

### Request

```jsonc
{
  "schema_version": 1,                      // required; only 1 is accepted
  "rule_id": "00000000-0000-0000-0000-000000000001", // keys sustained timers
  "node": { "type": "composite", "composite": { /* … */ } }, // full ConditionNode
  "root": "snooze",                         // optional root path segment;
                                            // defaults to the node's verbatim type
  "context": { /* ScenarioContext corpus shape */ },
  "now": "2026-01-05T12:00:00Z",
  "timers": {                               // optional; default {}
    "snooze": "2026-01-05T11:55:00Z"        // condition path -> first-true instant
  }
}
```

### Response

```jsonc
{
  "schema_version": 1,
  "ok": true,
  "value": true,                            // the node's truth
  "timers": { "snooze": "2026-01-05T12:00:00Z" },  // full post-state; persist verbatim
  "timer_ops": [                            // observable mutations, execution order
    { "op": "set", "path": "snooze", "at": "2026-01-05T12:00:00Z" }
  ]
}
```

Unknown node kinds and missing payloads evaluate `false` (silent-fail
parity). A structurally malformed `node` is an envelope error (`ok: false`) —
mirroring the C# callers, which all deserialise the tree (and skip on
`JsonException`) before dispatching into the registry. Timers are keyed by
the same `(rule_id, path)` identity as `evaluate`; sharing rows between the
per-reading and sweep variants of a scope is intentional (see
`docs/alerts/engine-semantics.md` §2.3).

## Leaf paths (`nocturne_alerts_leaf_paths`)

For timer-pruning hosts (`IConditionTimerStore.PruneToPathsAsync`): given a
condition tree, returns the canonical path of every node slot plus the
leaf-id/path pairs (`LeafIdentity.AssignLeafIds` pre-order ids).

Input is either a full ConditionNode object, or a wrapper that overrides the
root path segment (defaults to the node's verbatim `type` string, matching
`ConditionPath.Walk`; pass `"auto_resolve"` for auto-resolve trees):

```jsonc
{ "type": "composite", "composite": { … } }
// or
{ "root": "auto_resolve", "node": { "type": "sustained", … } }
```

Response:

```jsonc
{
  "schema_version": 1,
  "ok": true,
  "root": "composite",
  "paths": [                                  // every node slot, document order
    "composite",
    "composite[0].sustained",
    "composite[0].sustained[0].threshold",
    "composite[1].iob"
  ],
  "leaves": [                                  // pre-order leaf ids
    { "leaf_id": 0, "path": "composite[0].sustained[0].threshold" },
    { "leaf_id": 1, "path": "composite[1].iob" }
  ]
}
```

Container nodes whose payload/child is missing are leaves (the normative
`LeafIdentity` anomaly); a JSON-null child slot of a composite is a leaf whose
path has an empty type segment (`composite[2].`). Pruning timers to the
`paths` set is always safe — it is a superset of every path a timer can be
keyed under for that tree.

## Versioning

`schema_version` covers the envelope layer. Behavioural changes to evaluation
itself are governed by the golden corpus: both the Rust core (`tests/parity.rs`),
this crate's FFI round-trip test, and the .NET three-way suite
(`tests/Unit/Nocturne.Alerts.Native.Tests`) pin every scenario against the
committed `.expected.json` snapshots.
