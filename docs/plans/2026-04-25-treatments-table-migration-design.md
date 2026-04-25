# Treatments Table Migration — Design

## Goal

Remove the legacy `treatments` PostgreSQL table entirely, migrating all reads to V4 repositories and all writes through the TreatmentDecomposer. Same pattern as the entries table migration.

## Current State

- **Dual-write is active**: `DualPathTreatmentStore` decomposes new treatments into V4 records on write
- **V4 repos populated**: Bolus, CarbIntake, BGCheck, Note, BolusCalculation, DeviceEvent, TempBasal/StateSpan all receiving data
- **Projection exists**: `V4ToLegacyProjectionService` already projects Bolus, CarbIntake, BGCheck, Note, DeviceEvent back to Treatment shape — but only for V4-native records (null LegacyId)
- **Read path still legacy**: `DualPathTreatmentStore.QueryAsync` reads from `ITreatmentRepository` (legacy table), then merges V4 TempBasals

## Architecture

### TreatmentReadService

New `TreatmentReadService` (implements `ITreatmentStore`) replaces `DualPathTreatmentStore`. It:

1. Calls `V4ToLegacyProjectionService.GetProjectedTreatmentsAsync` with `nativeOnly: false` to project ALL V4 records (not just LegacyId-null)
2. Merges all projected treatments across all V4 types
3. Sorts by Mills descending, applies skip/take

### Projection Service Changes

Modify `V4ToLegacyProjectionService.GetProjectedTreatmentsAsync` to:

1. Accept `nativeOnly` parameter (default true for backwards compat during transition)
2. Add TempBasal projection (currently handled externally by `TempBasalToTreatmentMapper`)
3. Add BolusCalculation projection if missing
4. Add StateSpan-backed types (ProfileSwitch, TemporaryOverride, TemporaryTarget) if they need to appear as treatments

### Consumer Migration

| Consumer | Current dependency | Replacement |
|---|---|---|
| TreatmentService (patch, bulk delete) | `ITreatmentRepository` | V4 repos / `ITreatmentDecomposer.BulkDeleteAsync` |
| TimeQueryService (treatments path) | `ITreatmentRepository` | `ITreatmentService` |
| CountController (treatments count) | `ITreatmentRepository` | `ITreatmentStore.CountAsync` |
| V3 TreatmentsController | `ITreatmentRepository` | `ITreatmentService` |
| DataOverviewService | `_context.Treatments` | V4 repo queries (already partially done for entries) |
| DevAdminController | `_context.Treatments` | V4 repo counts |
| DeduplicationService | `_context.Treatments` | V4 repo lookups |
| DemoTreatmentService | `ITreatmentRepository` | `ITreatmentDecomposer` for writes |

### Dead Code Deletion

Once all consumers migrated:

- Delete `DualPathTreatmentStore`
- Delete `ITreatmentRepository`, `TreatmentRepository`
- Delete `TreatmentEntity`, `TreatmentMapper`
- Delete `TreatmentFoodEntity`, `TreatmentFoodRepository` (if food data lives in V4)
- Remove `DbSet<TreatmentEntity>` from `NocturneDbContext`
- Remove DI registrations
- DROP TABLE migration

## Decisions

- **Backfill is complete** — all legacy treatments have been decomposed to V4 tables via dual-write
- **`nativeOnly` parameter** on projection service allows gradual transition without breaking existing callers
- **Bulk delete** follows entries pattern — time-range only, NIGHTSCOUT-COMPAT guard
- **ParseTimeRangeFromFind** is shared (already fixed with $gt/$lt support in entries phase)
- **DualPathTreatmentStore's local ParseTimeRangeFromFind** should be removed in favor of `EntryDomainLogic.ParseTimeRangeFromFind`

## Phasing

1. Extend projection service (add TempBasal, nativeOnly param, BolusCalc, StateSpan types)
2. Build TreatmentReadService + CountAsync
3. Migrate consumers from ITreatmentRepository to ITreatmentService/ITreatmentStore
4. Add BulkDeleteAsync to ITreatmentDecomposer
5. Remove remaining _context.Treatments direct queries
6. Delete dead code + DROP TABLE migration
