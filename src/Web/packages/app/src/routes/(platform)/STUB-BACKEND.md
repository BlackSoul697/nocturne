# Outstanding Backend Stubs — Platform Roster

Grep for `STUB-BACKEND` to find all frontend stub locations.

| ID | Stub comment | Eventual endpoint | Pages affected |
|----|-------------|-------------------|----------------|
| 1 | `STUB-BACKEND: alarm count per tenant` | `GET /api/v4/platform/roster-snapshots` | Roster (AggregateStrip), Attention |
| 2 | `STUB-BACKEND: TIR percentages` | `GET /api/v4/platform/roster-snapshots` | Roster (TenantCard), Trends |
| 3 | `STUB-BACKEND: IOB/COB` | `GET /api/v4/platform/roster-snapshots` | Roster preview card |
| 4 | `STUB-BACKEND: 14-day TIR series` | `GET /api/v4/platform/trends` | Trends page |
| 5 | `STUB-BACKEND: cross-tenant event feed` | `GET /api/v4/platform/activity` | Activity page |
| 6 | `STUB-BACKEND: care plan data` | Per-tenant endpoint TBD | Care Plans page |

## Batch endpoint design note
All stubs 1–3 collapse into a single batch endpoint that returns per-tenant snapshots
including live BG, TIR, IOB, COB, and alarm counts. When that lands, the per-tenant
BG fan-out in `+layout.server.ts` is also replaced.
