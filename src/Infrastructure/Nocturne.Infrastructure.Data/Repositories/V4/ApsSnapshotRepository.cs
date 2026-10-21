using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.Audit;
using Nocturne.Core.Contracts.Events;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Entities.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;
using Nocturne.Infrastructure.Data.Services;
using Nocturne.Core.Contracts.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

/// <summary>
/// Repository for managing APS snapshots in the database. Takes the sync-key upsert and keyed delete
/// of <see cref="SyncUpsertRepositoryBase{TModel,TEntity}"/>, so it keeps only the APS-specific
/// queries.
/// </summary>
public class ApsSnapshotRepository : SyncUpsertRepositoryBase<ApsSnapshot, ApsSnapshotEntity>, IApsSnapshotRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApsSnapshotRepository"/> class.
    /// </summary>
    /// <param name="contextFactory">The tenant database context factory.</param>
    /// <param name="auditContext">The audit context for tracking mutations (used by the base soft-delete path).</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="broadcaster">Optional native V4 broadcaster; null disables broadcasting.</param>
    // logger is unused but retained for DI + direct test construction.
    public ApsSnapshotRepository(
        INocturneDbContextFactory contextFactory,
        IAuditContext auditContext,
        ILogger<ApsSnapshotRepository> logger,
        IV4RecordBroadcaster<ApsSnapshot>? broadcaster = null)
        : base(contextFactory, auditContext, logger, broadcaster)
    {
    }

    /// <inheritdoc />
    protected override ApsSnapshotEntity ToEntity(ApsSnapshot model) => ApsSnapshotMapper.ToEntity(model);

    /// <inheritdoc />
    protected override ApsSnapshot ToDomain(ApsSnapshotEntity entity) => ApsSnapshotMapper.ToDomainModel(entity);

    /// <inheritdoc />
    protected override void ApplyUpdate(ApsSnapshotEntity target, ApsSnapshot source) =>
        ApsSnapshotMapper.UpdateEntity(target, source);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApsIobCobPoint>> GetIobCobPointsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var ctx = await ContextFactory.CreateAsync(ct);
        return await ctx.ApsSnapshots.AsNoTracking()
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .OrderBy(e => e.Timestamp)
            .Select(e => new ApsIobCobPoint(e.Timestamp, e.Iob, e.Cob))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApsLoopCycle>> GetLoopCyclesAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        await using var ctx = await ContextFactory.CreateAsync(ct);
        var rows = await ctx.ApsSnapshots.AsNoTracking()
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .OrderBy(e => e.Timestamp)
            .Select(e => new
            {
                e.Timestamp,
                e.Device,
                e.AidAlgorithm,
                e.Enacted,
                e.EnactedRate,
                e.EnactedDuration,
                e.EnactedBolusVolume,
                e.Iob,
                e.SensitivityRatio,
                HasSuggestion = e.SuggestedJson != null,
                SuggestedChange = e.SuggestedJson == null
                    ? (bool?)null
                    : Jsonb.Text(e.SuggestedJson, "duration") != null || Jsonb.Text(e.SuggestedJson, "units") != null,
                LimitReason = Jsonb.Text(e.SuggestedJson, "reason")!.Contains("maxSafeBasal")
                    || Jsonb.Text(e.SuggestedJson, "reason")!.Contains("max_iob")
                        ? Jsonb.Text(e.SuggestedJson, "reason")
                        : null,
                MaxIob = Jsonb.Text(e.SuggestedJson, "max_iob"),
                MaxBasal = Jsonb.Text(e.SuggestedJson, "max_basal"),
                MaxDailySafetyMultiplier = Jsonb.Text(e.SuggestedJson, "max_daily_safety_multiplier"),
                CurrentBasalSafetyMultiplier = Jsonb.Text(e.SuggestedJson, "current_basal_safety_multiplier"),
            })
            .ToListAsync(ct);

        return [.. rows.Select(r => new ApsLoopCycle
        {
            Timestamp = r.Timestamp,
            Device = r.Device,
            AidAlgorithm = Enum.TryParse<AidAlgorithm>(r.AidAlgorithm, out var algorithm) ? algorithm : AidAlgorithm.Unknown,
            Enacted = r.Enacted,
            EnactedRate = r.EnactedRate,
            EnactedDuration = r.EnactedDuration,
            EnactedBolusVolume = r.EnactedBolusVolume,
            Iob = r.Iob,
            SensitivityRatio = r.SensitivityRatio,
            HasSuggestion = r.HasSuggestion,
            SuggestedChange = r.SuggestedChange,
            LimitReason = r.LimitReason,
            MaxIob = Number(r.MaxIob),
            MaxBasal = Number(r.MaxBasal),
            MaxDailySafetyMultiplier = Number(r.MaxDailySafetyMultiplier),
            CurrentBasalSafetyMultiplier = Number(r.CurrentBasalSafetyMultiplier),
        })];
    }

    /// <summary>A jsonb member's text as a number; null where it is absent or not a number.</summary>
    private static double? Number(string? text) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value)
            ? value
            : null;

    /// <summary>
    /// Gets APS snapshots by correlation IDs.
    /// </summary>
    /// <param name="correlationIds">The correlation IDs to match.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Matching APS snapshots.</returns>
    public async Task<IEnumerable<ApsSnapshot>> GetByCorrelationIdsAsync(
        IEnumerable<Guid> correlationIds, CancellationToken ct = default)
    {
        var ids = correlationIds.ToList();
        if (ids.Count == 0) return [];

        await using var ctx = await ContextFactory.CreateAsync(ct);
        var entities = await ctx.ApsSnapshots
            .AsNoTracking()
            .Where(e => e.CorrelationId != null && ids.Contains(e.CorrelationId.Value))
            .ToListAsync(ct);

        return entities.Select(ApsSnapshotMapper.ToDomainModel);
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLatestTimestampAsOfAsync(DateTime? asOf, CancellationToken ct = default)
    {
        await using var ctx = await ContextFactory.CreateAsync(ct);
        var query = ctx.ApsSnapshots.AsNoTracking();
        if (asOf.HasValue) query = query.Where(e => e.Timestamp <= asOf.Value);
        return await query
            .OrderByDescending(e => e.Timestamp)
            .Select(e => (DateTime?)e.Timestamp)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLatestEnactedTimestampAsync(DateTime? asOf, CancellationToken ct = default)
    {
        await using var ctx = await ContextFactory.CreateAsync(ct);
        var query = ctx.ApsSnapshots.AsNoTracking().Where(e => e.Enacted);
        if (asOf.HasValue) query = query.Where(e => e.Timestamp <= asOf.Value);
        return await query
            .OrderByDescending(e => e.Timestamp)
            .Select(e => (DateTime?)e.Timestamp)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Non-finite (Infinity/NaN) values from corrupt connector payloads are coerced to null rather than throwing.
    /// </remarks>
    public async Task<decimal?> GetLatestSensitivityRatioAsync(DateTime? asOf, CancellationToken ct = default)
    {
        await using var ctx = await ContextFactory.CreateAsync(ct);
        var query = ctx.ApsSnapshots.AsNoTracking().Where(e => e.SensitivityRatio != null);
        if (asOf.HasValue) query = query.Where(e => e.Timestamp <= asOf.Value);
        var value = await query
            .OrderByDescending(e => e.Timestamp)
            .Select(e => e.SensitivityRatio)
            .FirstOrDefaultAsync(ct);
        return value is double v && double.IsFinite(v) ? (decimal)v : null;
    }
}
