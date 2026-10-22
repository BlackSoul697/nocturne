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
        var snapshots = ctx.ApsSnapshots.AsNoTracking();
        var rows = await snapshots.Where(e => e.Timestamp < from).OrderByDescending(e => e.Timestamp).Take(1).Select(LoopCycleRow)
            .Concat(snapshots.Where(e => e.Timestamp >= from && e.Timestamp <= to).Select(LoopCycleRow))
            .Concat(snapshots.Where(e => e.Timestamp > to).OrderBy(e => e.Timestamp).Take(1).Select(LoopCycleRow))
            .OrderBy(r => r.Timestamp)
            .ToListAsync(ct);

        return [.. rows.Select(r =>
        {
            var reason = string.IsNullOrEmpty(r.Reason) ? null : r.Reason;
            return new ApsLoopCycle
            {
                Timestamp = r.Timestamp,
                AidAlgorithm = Enum.TryParse<AidAlgorithm>(r.AidAlgorithm, out var algorithm) ? algorithm : AidAlgorithm.Unknown,
                Enacted = r.Enacted,
                EnactedRate = r.EnactedRate,
                Iob = r.Iob,
                SensitivityRatio = r.SensitivityRatio,
                HasReason = reason is not null,
                SuggestedChange = r.SuggestedChange,
                LimitReason = reason is not null
                    && (reason.Contains("maxSafeBasal", StringComparison.Ordinal) || reason.Contains("max_iob", StringComparison.Ordinal))
                        ? reason
                        : null,
            };
        })];
    }

    /// <summary>Set by member initialisation, which EF can translate on both sides of a union.</summary>
    private sealed class LoopCycleColumns
    {
        public DateTime Timestamp { get; init; }
        public string AidAlgorithm { get; init; } = "";
        public bool Enacted { get; init; }
        public double? EnactedRate { get; init; }
        public double? Iob { get; init; }
        public double? SensitivityRatio { get; init; }
        public string? Reason { get; init; }
        public bool? SuggestedChange { get; init; }
    }

    /// <summary>
    /// The suggestion is read twice, for its reason and for whether it asked for a change, and the
    /// reason is matched here rather than in SQL: each read of a jsonb column detoasts it again.
    /// </summary>
    private static readonly System.Linq.Expressions.Expression<Func<ApsSnapshotEntity, LoopCycleColumns>> LoopCycleRow =
        e => new LoopCycleColumns
        {
            Timestamp = e.Timestamp,
            AidAlgorithm = e.AidAlgorithm,
            Enacted = e.Enacted,
            EnactedRate = e.EnactedRate,
            Iob = e.Iob,
            SensitivityRatio = e.SensitivityRatio,
            Reason = Jsonb.Text(e.SuggestedJson, "reason"),
            SuggestedChange = e.SuggestedJson == null ? null : EF.Functions.JsonExistAny(e.SuggestedJson, "duration", "units"),
        };

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
