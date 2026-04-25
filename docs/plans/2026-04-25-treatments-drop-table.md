# Treatments Table Migration — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove the legacy `treatments` PostgreSQL table, migrating all reads to V4 repository projections and all writes through the TreatmentDecomposer.

**Architecture:** Extend `V4ToLegacyProjectionService` to project ALL V4 records (not just native), add TempBasal/BolusCalc/StateSpan projection, build `TreatmentReadService` as the V4-only `ITreatmentStore`, migrate remaining consumers, delete dead code, DROP TABLE.

**Tech Stack:** C# / .NET 10, EF Core, xUnit, Moq, FluentAssertions

**Worktree:** `c:/Users/rhysg/Documents/Github/nocturne/.worktrees/drop-entries-table` (branch: `feature/v1-deprecation-treatments`)

---

### Grill Findings

Resolved during design review:
1. **V4 TreatmentsController** — delete entirely. V4 clients use granular endpoints (BolusController, NutritionController, etc.), not the legacy Treatment shape.
2. **BolusCalculation projection gap** — `V4ToLegacyProjectionService` doesn't project BolusCalculation → "Bolus Wizard" treatment. Must add in Task 1.
3. **ServicesController** — uses `ITreatmentRepository` for timestamp queries. Swap to V4 repos.
4. **`GetUnifiedTreatmentAsync`** in DeduplicationService — zero callers, dead code. Delete.
5. **Notes gap in decomposer** — When a non-Note treatment (e.g. "Meal Bolus") has non-empty `Notes`, the decomposer does NOT produce a Note V4 record. Those notes are lost in V4. Fix by producing a correlated Note record whenever `Treatment.Notes` is non-empty, regardless of event type. This must happen before dropping the table so existing data can be backfilled.
6. **`DualPathTreatmentStore.ParseTimeRangeFromFind`** — duplicate of `EntryDomainLogic.ParseTimeRangeFromFind`. Unify during cleanup.

---

### Task 0: Fix decomposer Notes gap and backfill

The `TreatmentDecomposer` only produces a Note V4 record for `EventType == "Note"` or `"Announcement"`. When a "Meal Bolus", "Correction Bolus", "BG Check", or any other treatment has non-empty `Notes`, that text is lost in V4. Fix this so Notes are preserved as correlated Note records.

**Files:**
- Modify: `src/API/Nocturne.API/Services/V4/TreatmentDecomposer.cs`
- Create: `tests/Unit/Nocturne.API.Tests/Services/V4/TreatmentDecomposerNotesTests.cs`

**Step 1: Write failing test**

Test that decomposing a "Meal Bolus" with `Notes = "birthday cake"` produces both a Bolus AND a Note V4 record linked by CorrelationId.

**Step 2: Fix the decomposer**

After all produce flags are set (around line 215), add:

```csharp
// Produce a Note record for any treatment with non-empty Notes,
// unless we're already producing a Note (avoids duplicate).
if (!produceNote && !string.IsNullOrWhiteSpace(treatment.Notes))
{
    produceNote = true;
}
```

This ensures the Notes text is preserved as a V4 Note record linked via CorrelationId to the other V4 records from the same treatment.

**Step 3: Run tests, commit**

```bash
dotnet test --filter "FullyQualifiedName~TreatmentDecomposer" --no-restore
git commit -m "fix(treatments): produce Note record for any treatment with non-empty Notes"
```

**Step 4: Backfill existing treatments**

Existing legacy treatments with Notes that were dual-written before this fix will have lost their notes in V4. Run a one-time re-decompose of treatments with non-empty Notes from the legacy table to backfill the missing Note records. This can use the existing `V4BackfillService` pattern or a simple script that:
1. Queries `_context.Treatments.Where(t => t.Notes != null && t.Notes != "")`
2. Maps each to a domain Treatment via `TreatmentMapper.ToDomainModel`
3. Re-decomposes via `TreatmentDecomposer.DecomposeAsync` (idempotent via LegacyId matching)

This must happen BEFORE dropping the treatments table.

```bash
git commit -m "fix(treatments): backfill missing Note records for treatments with Notes"
```

---

### Task 1: Extend V4ToLegacyProjectionService with nativeOnly parameter, TempBasal, and BolusCalculation projection

The projection service currently only projects V4-native records (null LegacyId). Add a `nativeOnly` parameter so `TreatmentReadService` can request all records. Also add TempBasal and BolusCalculation projection which are currently missing.

**Files:**
- Modify: `src/Core/Nocturne.Core.Contracts/V4/IV4ToLegacyProjectionService.cs`
- Modify: `src/API/Nocturne.API/Services/V4/V4ToLegacyProjectionService.cs`

**Step 1: Add nativeOnly parameter to interface**

In `IV4ToLegacyProjectionService.cs`, change the `GetProjectedTreatmentsAsync` signature:

```csharp
Task<IEnumerable<Treatment>> GetProjectedTreatmentsAsync(
    long? fromMills,
    long? toMills,
    int limit,
    bool nativeOnly = true,
    CancellationToken ct = default
);
```

**Step 2: Inject ITempBasalRepository and IBolusCalculationRepository into V4ToLegacyProjectionService**

Add constructor parameters and fields for `ITempBasalRepository` and `IBolusCalculationRepository`.

**Step 3: Update GetProjectedTreatmentsAsync implementation**

1. Pass `nativeOnly` through to each `FetchSafe` call. When `nativeOnly` is false, don't filter by `LegacyId == null` — fetch all records in the time range.

2. Add TempBasal fetching and projection:
```csharp
var tempBasals = await FetchSafe("TempBasal", async () =>
{
    var results = await _tempBasalRepository.GetAsync(from, to, device: null, source: null,
        limit: limit, offset: 0, descending: true, ct: ct);
    return nativeOnly ? results.Where(r => r.LegacyId == null) : results;
}, ct);

treatments.AddRange(tempBasals.Select(TempBasalToTreatmentMapper.ToTreatment));
```

3. Add BolusCalculation fetching and projection — project to Treatment with EventType "Bolus Wizard". Map BolusCalculation fields (BloodGlucoseInput, CarbInput, InsulinRecommendation, InsulinOnBoard, etc.) back to the Treatment's BolusCalc dictionary and bolus calculator fields.

4. Add `IBolusCalculationRepository` as a constructor dependency.

**Step 4: Build and verify**

```bash
dotnet build 2>&1 | tail -5
```

**Step 5: Commit**

```bash
git commit -m "feat(treatments): extend projection service with nativeOnly param and TempBasal projection"
```

---

### Task 2: Build TreatmentReadService

Create a V4-only `ITreatmentStore` implementation analogous to `EntryReadService`.

**Files:**
- Create: `src/API/Nocturne.API/Services/Treatments/TreatmentReadService.cs`
- Create: `tests/Unit/Nocturne.API.Tests/Services/Treatments/TreatmentReadServiceTests.cs`

**Step 1: Write tests**

Test that `TreatmentReadService.QueryAsync` delegates to the projection service with `nativeOnly: false`, and that `CountAsync` sums across V4 repos.

**Step 2: Implement TreatmentReadService**

```csharp
public class TreatmentReadService : ITreatmentStore
{
    private readonly IV4ToLegacyProjectionService _projection;
    private readonly ITreatmentDecomposer _decomposer;
    private readonly IDecompositionPipeline _pipeline;
    // V4 repos for count, getById, etc.
}
```

Key methods:
- `QueryAsync(TreatmentQuery)`: Parse time range from `query.Find`, call `_projection.GetProjectedTreatmentsAsync(from, to, count, nativeOnly: false)`, apply skip/take
- `GetByIdAsync(id)`: Search across V4 repos by ID/LegacyId, project to Treatment
- `GetModifiedSinceAsync(mills, limit)`: Query V4 repos by SysUpdatedAt, project results
- `CreateAsync(treatments)`: Route through `_pipeline.DecomposeAsync` (V4-only writes, no legacy table)
- `UpdateAsync(id, treatment)`: Find existing V4 record, decompose updated treatment
- `DeleteAsync(id)`: Call `_pipeline.DeleteByLegacyIdAsync`

**Step 3: Run tests**

```bash
dotnet test --filter "FullyQualifiedName~TreatmentReadServiceTests" --no-restore
```

**Step 4: Commit**

```bash
git commit -m "feat(treatments): add TreatmentReadService backed by V4 projections"
```

---

### Task 3: Add CountAsync to ITreatmentStore

Same pattern as entries — sum counts from all treatment-related V4 repos.

**Files:**
- Modify: `src/Core/Nocturne.Core.Contracts/Treatments/ITreatmentStore.cs`
- Modify: `src/API/Nocturne.API/Services/Treatments/TreatmentReadService.cs`
- Create: `tests/Unit/Nocturne.API.Tests/Services/Treatments/TreatmentReadServiceCountTests.cs`

**Step 1: Add to interface**

```csharp
Task<long> CountAsync(string? find = null, CancellationToken ct = default);
```

**Step 2: Implement**

Parse time range from find, then sum counts from Bolus + CarbIntake + BGCheck + Note + BolusCalculation + DeviceEvent + TempBasal repos. Use each repo's `CountAsync(from, to)`.

**Step 3: Write tests, run, commit**

```bash
git commit -m "feat(treatments): add CountAsync to ITreatmentStore backed by V4 repos"
```

---

### Task 4: Wire TreatmentReadService as ITreatmentStore in DI

Replace `DualPathTreatmentStore` with `TreatmentReadService` in DI registration.

**Files:**
- Modify: `src/API/Nocturne.API/Extensions/ServiceRegistrationExtensions.cs`

**Step 1: Find the DualPathTreatmentStore registration**

Look for `services.AddScoped<ITreatmentStore, DualPathTreatmentStore>()`. Change to:
```csharp
services.AddScoped<ITreatmentStore, TreatmentReadService>();
```

**Step 2: Build and run tests**

Full build + unit test run to verify nothing breaks from the swap.

```bash
dotnet build 2>&1 | tail -5
dotnet test --filter "Category!=Integration&Category!=Performance&FullyQualifiedName!~GoldenFiles&FullyQualifiedName!~SetupController&FullyQualifiedName!~AuthenticationController&FullyQualifiedName!~TenantIsolation&FullyQualifiedName!~OidcCallback&FullyQualifiedName!~Parity&FullyQualifiedName!~DataHub&FullyQualifiedName!~SignalR&FullyQualifiedName!~AuthorizationManagement&FullyQualifiedName!~AuthenticationHandler" 2>&1 | tail -5
```

**Step 3: Commit**

```bash
git commit -m "refactor(treatments): wire TreatmentReadService as ITreatmentStore, replacing DualPathTreatmentStore"
```

---

### Task 5: Migrate TreatmentService from ITreatmentRepository

`TreatmentService` uses `ITreatmentRepository` directly for two operations:
- `PatchTreatmentAsync` (line 180)
- `DeleteTreatmentsAsync` / `BulkDeleteTreatmentsAsync` (line 211)

**Files:**
- Modify: `src/Core/Nocturne.Core.Contracts/V4/ITreatmentDecomposer.cs`
- Modify: `src/API/Nocturne.API/Services/V4/TreatmentDecomposer.cs`
- Modify: `src/API/Nocturne.API/Services/Treatments/TreatmentService.cs`
- Modify: `tests/Unit/Nocturne.API.Tests/Services/Treatments/TreatmentServiceTests.cs`

**Step 1: Add BulkDeleteAsync to ITreatmentDecomposer**

Same pattern as entries — parse time range from find, delete across V4 repos. Each treatment V4 repo needs `DeleteByTimeRangeAsync` added (Bolus, CarbIntake, BGCheck, Note, BolusCalculation, DeviceEvent repos). TempBasal/StateSpan already has time-range operations.

```csharp
Task<long> BulkDeleteAsync(string? find, CancellationToken ct = default);
```

Implementation: Parse time range, delete from all 7+ repos, sum results. Include the NIGHTSCOUT-COMPAT safety guard (same as entries).

**Step 2: Add PatchAsync to ITreatmentDecomposer or handle via decompose**

For patch: fetch the existing Treatment via store, apply patch fields, then re-decompose. This is simpler than trying to patch individual V4 records since Treatment is a grab bag that decomposes into multiple types.

```csharp
// In TreatmentService.PatchTreatmentAsync:
var existing = await _store.GetByIdAsync(id, ct);
if (existing == null) return null;
// Apply patch fields to existing
foreach (var (key, value) in patchData)
    ApplyPatch(existing, key, value);
// Re-decompose (idempotent upsert via LegacyId matching)
await _decomposer.DecomposeAsync(existing, ct);
return existing;
```

**Step 3: Remove ITreatmentRepository from TreatmentService**

Remove field, constructor param, using directive. Update tests.

**Step 4: Run tests, commit**

```bash
git commit -m "refactor(treatments): remove ITreatmentRepository from TreatmentService"
```

---

### Task 6: Migrate V3 TreatmentsController count from ITreatmentRepository

The V3 controller uses `_treatments.CountTreatmentsAsync()` in `GetTotalCountAsync` (line 669).

**Files:**
- Modify: `src/API/Nocturne.API/Controllers/V3/TreatmentsController.cs`

**Step 1: Replace ITreatmentRepository with ITreatmentStore for count**

Replace `_treatments.CountTreatmentsAsync(findQuery, ct)` with `_treatmentStore.CountAsync(findQuery, ct)`. Add `ITreatmentStore` as a constructor dependency (or use `ITreatmentService` if count is added there).

Remove `ITreatmentRepository` from the controller if no longer used.

**Step 2: Build, test, commit**

```bash
git commit -m "refactor(treatments): migrate V3 TreatmentsController from ITreatmentRepository to ITreatmentStore"
```

---

### Task 7: Migrate TimeQueryService treatments path

`TimeQueryService` calls `_treatments.GetTreatmentsWithAdvancedFilterAsync()` for the treatments storage type in both `ExecuteTimeQueryAsync` and `ExecuteSliceQueryAsync`.

**Files:**
- Modify: `src/API/Nocturne.API/Services/Platform/TimeQueryService.cs`
- Modify: `tests/Unit/Nocturne.API.Tests/Services/Platform/TimeQueryServiceTests.cs`

**Step 1: Replace ITreatmentRepository with ITreatmentService**

Same pattern as entries Phase 5d. Change constructor dependency, update the treatments case in both methods to call `ITreatmentService.GetTreatmentsWithAdvancedFilterAsync`.

**Step 2: Update tests, run, commit**

```bash
git commit -m "refactor(treatments): migrate TimeQueryService treatments path from ITreatmentRepository to ITreatmentService"
```

---

### Task 8: Migrate CountController treatments count

The `CountController.CountTreatments` and `CountGeneric` "treatments" case call `_treatmentRepository.CountTreatmentsAsync`.

**Files:**
- Modify: `src/API/Nocturne.API/Controllers/V1/CountController.cs`

**Step 1: Add ITreatmentStore, replace treatments count calls**

Replace `_treatmentRepository.CountTreatmentsAsync(find, ct)` with an `ITreatmentStore.CountAsync(find, ct)` call. Remove `ITreatmentRepository` from the controller if no longer needed.

**Step 2: Build, test, commit**

```bash
git commit -m "refactor(treatments): migrate CountController treatments count from ITreatmentRepository to ITreatmentStore"
```

---

### Task 9: Remove _context.Treatments from DataOverviewService, DevAdminController, DeduplicationService

Same pattern as entries Phase 6. These services query `_context.Treatments` directly.

**Files:**
- Modify: `src/API/Nocturne.API/Services/Analytics/DataOverviewService.cs`
- Modify: `src/API/Nocturne.API/Controllers/V4/DevOnly/DevAdminController.cs`
- Modify: `src/Infrastructure/Nocturne.Infrastructure.Data/Services/DeduplicationService.cs`

**DataOverviewService:** Remove `_context.Treatments` queries — the V4 table queries (Boluses, CarbIntakes, etc.) already cover this data since the continuation doc confirms backfill is complete.

**DevAdminController:** Replace `_db.Treatments.LongCountAsync()` with sum of V4 treatment repo counts. Replace latest treatment timestamp query with V4 repo query.

**DeduplicationService:** Same pattern as entries — remove `RecordType.Treatment` branch from `GetOrCreateCanonicalIdAsync` (only `TreatmentRepository` calls with that type), remove `"treatment"` case from `RecordExistsAsync`, delete `GetUnifiedTreatmentAsync` if zero callers, refactor `DeduplicateTreatmentsAsync` to scan V4 tables.

**Commit per file or batch:**

```bash
git commit -m "refactor(treatments): remove _context.Treatments direct queries from consumer services"
```

---

### Task 10: Migrate remaining ITreatmentRepository consumers

Grep for any remaining `ITreatmentRepository` references outside of the repository itself. Likely candidates:
- `DemoTreatmentService` — migrate to `ITreatmentDecomposer` for writes
- `DemoDataHostedService` / `DemoServiceHealthMonitor` — update DI
- `DataSourceService` — replace with V4 repo queries
- `DDataService` / `PredictionService` — migrate to `ITreatmentService`
- `ServicesController` — update metadata
- `CacheWarmingService` — update to use ITreatmentStore

For each: read the file, determine what ITreatmentRepository method is used, replace with the V4 equivalent.

**Commit:**

```bash
git commit -m "refactor(treatments): migrate remaining ITreatmentRepository consumers to V4 path"
```

---

### Task 11: Delete dead code

Once all consumers migrated:

**Files to DELETE:**
- `src/Core/Nocturne.Core.Contracts/Repositories/ITreatmentRepository.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/Repositories/TreatmentRepository.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/TreatmentEntity.cs` (and owned types)
- `src/Infrastructure/Nocturne.Infrastructure.Data/Mappers/TreatmentMapper.cs`
- `src/API/Nocturne.API/Services/Treatments/DualPathTreatmentStore.cs`
- `src/API/Nocturne.API/Controllers/V4/Treatments/TreatmentsController.cs` (V4 clients use granular endpoints)
- `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/TreatmentFoodEntity.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/Repositories/TreatmentFoodRepository.cs`
- Related test files

**Files to MODIFY:**
- `NocturneDbContext.cs` — remove `DbSet<TreatmentEntity> Treatments`, `DbSet<TreatmentFoodEntity> TreatmentFoods`, all `modelBuilder.Entity<TreatmentEntity>()` configs
- `ServiceCollectionExtensions.cs` — remove DI registrations
- Fix all compilation errors from deleted references

**Build, fix, test, commit:**

```bash
git commit -m "refactor: delete ITreatmentRepository, TreatmentRepository, TreatmentEntity, TreatmentMapper, DualPathTreatmentStore"
```

---

### Task 12: Scaffold DROP TABLE migration

```bash
dotnet build -p:GenerateNSwagClient=false
dotnet ef migrations add DropTreatmentsTable \
    -p src/Infrastructure/Nocturne.Infrastructure.Data \
    -s src/API/Nocturne.API \
    --no-build
```

Verify the migration drops `treatments` and `treatment_foods` tables.

```bash
git commit -m "feat(db): add DropTreatmentsTable migration"
```

---

### Task 13: Final verification

```bash
# No remaining references
grep -rn "TreatmentEntity\|ITreatmentRepository\|TreatmentRepository\|TreatmentMapper\|_context\.Treatments\|\.Treatments\." src/ --include="*.cs" | grep -v "Migrations/" | grep -v "//" | grep -v "TreatmentFood"

# Build
dotnet build 2>&1 | tail -5

# Tests
dotnet test --filter "Category!=Integration&Category!=Performance&FullyQualifiedName!~GoldenFiles&FullyQualifiedName!~SetupController&FullyQualifiedName!~AuthenticationController&FullyQualifiedName!~TenantIsolation&FullyQualifiedName!~OidcCallback&FullyQualifiedName!~Parity&FullyQualifiedName!~DataHub&FullyQualifiedName!~SignalR&FullyQualifiedName!~AuthorizationManagement&FullyQualifiedName!~AuthenticationHandler" 2>&1 | tail -5
```
