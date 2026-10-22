using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Extensions;
using Npgsql;
using NpgsqlTypes;

namespace Nocturne.Infrastructure.Data.Services;

/// <summary>
/// Settles the temp basals whose origin is a default (<see cref="TempBasalOriginProvenance"/>)
/// against the APS snapshots their device uploaded around their start, links the snapshot that
/// proposed each, and records the <see cref="TempBasalOriginEvidence"/> it rests on.
/// </summary>
/// <remarks>
/// <para>
/// Devicestatus and treatments arrive separately, so this runs on stored rows once both have
/// settled rather than at ingest. A row stored before evidence was recorded is classified first by
/// <see cref="TempBasalOriginProvenance.EvidenceOf(TempBasalOrigin, TempBasalOriginEvidence?, bool?)"/>:
/// a stated origin is marked <see cref="TempBasalOriginEvidence.Reported"/> and left as it is, and a
/// default is replaced by its uploader's current default before it is judged.
/// </para>
/// <para>
/// A tenant's pending rows (<c>ix_temp_basals_origin_pending</c>) are walked in pages along
/// <c>(start_timestamp, id)</c>, and a judged row leaves that index, so a rerun reads only rows
/// written since. Each row is written only if its <c>sys_updated_at</c> and evidence are those it
/// was judged on, so an upload that rewrote it meanwhile stands and is judged on a later run.
/// <c>sys_updated_at</c> is left alone, so v3 history does not resend every judged temp basal.
/// </para>
/// </remarks>
public static class TempBasalOriginCorroborator
{
    /// <summary>How far a snapshot or its enactment may be from a temp basal's start to judge it.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How much later than the window a snapshot is read, for an enactment Trio repeats in the
    /// snapshots after the one that made it.
    /// </summary>
    private static readonly TimeSpan RepeatedEnactment = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Just under the 0.05 U/h step of the coarsest pumps, which deliver a proposed rate rounded to
    /// their step: a rate one whole step from the proposal is a different rate.
    /// </summary>
    private const double RateTolerance = 0.0495;

    private const string PageSql = """
        SELECT id, start_timestamp, rate, device, app, origin, origin_evidence,
               additional_properties ->> 'automatic', aps_snapshot_id, sys_updated_at
        FROM temp_basals
        WHERE tenant_id = @tenant
          AND deleted_at IS NULL AND (origin_evidence IS NULL OR origin_evidence = 'Uploader')
          AND sys_updated_at < @settled
          AND (start_timestamp, id) > (@after_at, @after_id)
        ORDER BY start_timestamp, id
        LIMIT @size
        """;

    private const string ProposalSql = """
        SELECT DISTINCT ON (a.id) a.id, a.device, a.timestamp,
               COALESCE(a.enacted_json ->> 'timestamp', a.loop_json #>> '{enacted,timestamp}'),
               CASE WHEN a.enacted THEN a.enacted_rate END,
               COALESCE(a.suggested_json ->> 'rate',
                        a.loop_json #>> '{automaticDoseRecommendation,tempBasal,rate}',
                        a.loop_json #>> '{recommendedTempBasal,rate}')
        FROM unnest(@starts, @devices) AS w(start, device)
        JOIN aps_snapshots a
          ON a.device = w.device AND a.timestamp >= w.start - @before AND a.timestamp <= w.start + @after
        WHERE a.tenant_id = @tenant AND a.deleted_at IS NULL
          AND a.timestamp >= @from AND a.timestamp <= @to
        ORDER BY a.id
        """;

    private const string WriteSql = """
        UPDATE temp_basals t
        SET origin = u.origin, origin_evidence = u.evidence, aps_snapshot_id = u.snapshot
        FROM unnest(@ids, @origins, @evidences, @snapshots, @read_at, @read_evidence)
            AS u(id, origin, evidence, snapshot, read_at, read_evidence)
        WHERE t.id = u.id AND t.tenant_id = @tenant AND t.deleted_at IS NULL
          AND t.sys_updated_at = u.read_at
          AND t.origin_evidence IS NOT DISTINCT FROM u.read_evidence
        """;

    /// <summary>What a snapshot proposed, and when.</summary>
    /// <param name="SnapshotId">The snapshot.</param>
    /// <param name="Timestamp">The snapshot's time.</param>
    /// <param name="EnactedAt">The time its enactment names, which Trio may repeat from an earlier snapshot.</param>
    /// <param name="EnactedRate">The rate it enacted; null when it enacted none.</param>
    /// <param name="SuggestedRate">The rate it suggested, enacted or not.</param>
    public sealed record Proposal(
        Guid SnapshotId, DateTime Timestamp, DateTime? EnactedAt, double? EnactedRate, double? SuggestedRate);

    public sealed record Verdict(TempBasalOrigin Origin, TempBasalOriginEvidence Evidence, Guid? SnapshotId);

    /// <summary>
    /// Judges a temp basal against the proposals of its own device. One within <see cref="Window"/>
    /// of its start, by the snapshot's time or its enactment's, that enacted or suggested its rate
    /// makes it the loop's, linked to the nearest such. Proposals nearby that all proposed another
    /// rate mean the loop was running and the temp was set by hand. With none nearby, or no rate to
    /// compare, <paramref name="uploaderDefault"/> stands.
    /// </summary>
    public static Verdict Judge(
        TempBasalOrigin uploaderDefault, DateTime start, double? rate, IEnumerable<Proposal> proposals)
    {
        var near = proposals
            .Select(p => (Proposal: p, Distance: DistanceFrom(p, start)))
            .Where(p => p.Distance <= Window)
            .ToList();
        if (near.Count == 0 || rate is not { } r)
            return new Verdict(uploaderDefault, TempBasalOriginEvidence.Uncorroborated, null);

        var match = near
            .Where(p => Same(p.Proposal.EnactedRate, r) || Same(p.Proposal.SuggestedRate, r))
            .OrderBy(p => p.Distance)
            .Select(p => p.Proposal)
            .FirstOrDefault();

        return match is null
            ? new Verdict(TempBasalOrigin.Manual, TempBasalOriginEvidence.Snapshot, null)
            : new Verdict(TempBasalOrigin.Algorithm, TempBasalOriginEvidence.Snapshot, match.SnapshotId);
    }

    /// <summary>
    /// Judges every settled pending temp basal of the tenant <paramref name="db"/> is pinned to, a
    /// page at a time, and answers how many it wrote. Each page commits on its own, retried on a
    /// transient failure.
    /// </summary>
    /// <param name="db">A context pinned to <paramref name="tenantId"/>.</param>
    /// <param name="tenantId">The tenant whose temp basals to judge.</param>
    /// <param name="settledBefore">Rows written at or after this are left for a later run.</param>
    /// <param name="pageSize">Temp basals read per page.</param>
    /// <param name="pauseAfterWrite">The pause after a page that wrote.</param>
    /// <param name="pauseAfterRead">The pause after a page that wrote nothing.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<int> CorroborateTenantAsync(
        NocturneDbContext db,
        Guid tenantId,
        DateTime settledBefore,
        int pageSize,
        TimeSpan pauseAfterWrite,
        TimeSpan pauseAfterRead,
        CancellationToken ct)
    {
        var afterAt = DateTime.UnixEpoch;
        var afterId = Guid.Empty;
        var written = 0;

        while (true)
        {
            var page = await db.ExecuteInTransactionAsync(
                attemptCt => CorroboratePageAsync(db, tenantId, settledBefore, afterAt, afterId, pageSize, attemptCt),
                ct: ct);
            if (page is not { } done)
                return written;

            (afterAt, afterId) = (done.LastAt, done.LastId);
            written += done.Written;
            await Task.Delay(done.Written > 0 ? pauseAfterWrite : pauseAfterRead, ct);
        }
    }

    private sealed record Row(
        Guid Id,
        DateTime Start,
        double? Rate,
        string? Device,
        string? App,
        TempBasalOrigin Origin,
        string? Evidence,
        bool? AutomaticFlag,
        Guid? SnapshotId,
        DateTime ReadAt);

    private static async Task<(int Written, DateTime LastAt, Guid LastId)?> CorroboratePageAsync(
        NocturneDbContext db,
        Guid tenantId,
        DateTime settledBefore,
        DateTime afterAt,
        Guid afterId,
        int pageSize,
        CancellationToken ct)
    {
        var rows = await ReadPageAsync(db, tenantId, settledBefore, afterAt, afterId, pageSize, ct);
        if (rows.Count == 0)
            return null;

        var proposals = await ReadProposalsAsync(db, tenantId, rows.Where(r => r.Device is not null).ToList(), ct);

        var verdicts = rows.Select(row => (Row: row, Verdict: Settle(row, proposals))).ToList();
        var written = await WriteAsync(db, tenantId, verdicts, ct);
        return (written, rows[^1].Start, rows[^1].Id);
    }

    private static Verdict Settle(Row row, ILookup<string, Proposal> proposals)
    {
        var stored = Enum.TryParse<TempBasalOriginEvidence>(row.Evidence, out var parsed) ? parsed : (TempBasalOriginEvidence?)null;
        if (TempBasalOriginProvenance.EvidenceOf(row.Origin, stored, row.AutomaticFlag) == TempBasalOriginEvidence.Reported)
            return new Verdict(row.Origin, TempBasalOriginEvidence.Reported, row.SnapshotId);

        // Its uploader named the snapshot that set it.
        if (row.SnapshotId is { } linked)
            return new Verdict(row.Origin, TempBasalOriginEvidence.Snapshot, linked);

        var uploaderDefault = stored is null
            ? TempBasalOriginProvenance.UploaderDefault(row.App, app: null)
            : row.Origin;
        return Judge(uploaderDefault, row.Start, row.Rate, row.Device is null ? [] : proposals[row.Device]);
    }

    private static TimeSpan DistanceFrom(Proposal proposal, DateTime start)
    {
        var distance = (proposal.Timestamp - start).Duration();
        return proposal.EnactedAt is { } enactedAt && (enactedAt - start).Duration() < distance
            ? (enactedAt - start).Duration()
            : distance;
    }

    private static bool Same(double? proposed, double rate) =>
        proposed is { } p && Math.Abs(p - rate) < RateTolerance;

    private static async Task<List<Row>> ReadPageAsync(
        NocturneDbContext db,
        Guid tenantId,
        DateTime settledBefore,
        DateTime afterAt,
        Guid afterId,
        int pageSize,
        CancellationToken ct)
    {
        await using var command = Command(db, PageSql);
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("settled", settledBefore);
        command.Parameters.AddWithValue("after_at", afterAt);
        command.Parameters.AddWithValue("after_id", afterId);
        command.Parameters.AddWithValue("size", pageSize);

        var rows = new List<Row>(pageSize);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new Row(
                reader.GetGuid(0),
                reader.GetDateTime(1),
                reader.IsDBNull(2) ? null : reader.GetDouble(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                Enum.TryParse<TempBasalOrigin>(reader.GetString(5), out var origin) ? origin : TempBasalOrigin.Inferred,
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : bool.TryParse(reader.GetString(7), out var flag) ? flag : null,
                reader.IsDBNull(8) ? null : reader.GetGuid(8),
                reader.GetDateTime(9)));
        }

        return rows;
    }

    private static async Task<ILookup<string, Proposal>> ReadProposalsAsync(
        NocturneDbContext db, Guid tenantId, List<Row> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return Array.Empty<(string, Proposal)>().ToLookup(p => p.Item1, p => p.Item2);

        await using var command = Command(db, ProposalSql);
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.Add(new NpgsqlParameter("starts", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz)
        {
            Value = rows.Select(r => r.Start).ToArray(),
        });
        command.Parameters.AddWithValue("devices", rows.Select(r => r.Device!).ToArray());
        command.Parameters.AddWithValue("before", Window);
        command.Parameters.AddWithValue("after", Window + RepeatedEnactment);
        command.Parameters.AddWithValue("from", rows[0].Start - Window);
        command.Parameters.AddWithValue("to", rows[^1].Start + Window + RepeatedEnactment);

        var proposals = new List<(string Device, Proposal Proposal)>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            proposals.Add((reader.GetString(1), new Proposal(
                reader.GetGuid(0),
                reader.GetDateTime(2),
                reader.IsDBNull(3) ? null : ParseInstant(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetDouble(4),
                reader.IsDBNull(5) ? null : ParseRate(reader.GetString(5)))));
        }

        return proposals.ToLookup(p => p.Device, p => p.Proposal, StringComparer.Ordinal);
    }

    private static async Task<int> WriteAsync(
        NocturneDbContext db, Guid tenantId, List<(Row Row, Verdict Verdict)> verdicts, CancellationToken ct)
    {
        await using var command = Command(db, WriteSql);
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("ids", verdicts.Select(v => v.Row.Id).ToArray());
        command.Parameters.AddWithValue("origins", verdicts.Select(v => v.Verdict.Origin.ToString()).ToArray());
        command.Parameters.AddWithValue("evidences", verdicts.Select(v => v.Verdict.Evidence.ToString()).ToArray());
        command.Parameters.Add(new NpgsqlParameter("snapshots", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = verdicts.Select(v => v.Verdict.SnapshotId is { } id ? (object)id : DBNull.Value).ToArray(),
        });
        command.Parameters.Add(new NpgsqlParameter("read_at", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz)
        {
            Value = verdicts.Select(v => v.Row.ReadAt).ToArray(),
        });
        command.Parameters.Add(new NpgsqlParameter("read_evidence", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = verdicts.Select(v => v.Row.Evidence is { } e ? (object)e : DBNull.Value).ToArray(),
        });

        return await command.ExecuteNonQueryAsync(ct);
    }

    // Plain ADO.NET on the context's connection, which carries its tenant: EF's raw SQL would read
    // the JSON paths' braces as format placeholders.
    private static NpgsqlCommand Command(NocturneDbContext db, string sql) =>
        new(sql, (NpgsqlConnection)db.Database.GetDbConnection())
        {
            Transaction = (NpgsqlTransaction?)db.Database.CurrentTransaction?.GetDbTransaction(),
        };

    private static DateTime? ParseInstant(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant)
            ? instant.UtcDateTime
            : null;

    private static double? ParseRate(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) ? rate : null;
}
