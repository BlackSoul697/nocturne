using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Core.Contracts.Sleep;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Sleep.Report;

namespace Nocturne.API.Services.Sleep;

/// <summary>
/// Orchestrates sleep report data by combining session records with CGM readings
/// and delegating all computation to <see cref="SleepReportCalculator"/>.
/// </summary>
public class SleepReportService : ISleepReportService
{
    private readonly ISleepSessionRepository _sessions;
    private readonly ISensorGlucoseRepository _glucose;
    private readonly ILogger<SleepReportService> _logger;

    public SleepReportService(
        ISleepSessionRepository sessions,
        ISensorGlucoseRepository glucose,
        ILogger<SleepReportService> logger)
    {
        _sessions = sessions;
        _glucose  = glucose;
        _logger   = logger;
    }

    /// <inheritdoc/>
    public async Task<SleepSingleNightReport?> GetSingleNightReportAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        var session = await _sessions.GetSessionByIdAsync(sessionId, ct);
        if (session is null)
            return null;

        var glucoseReadings = await _glucose.GetAsync(
            from:           session.StartTime,
            to:             session.EndTime,
            device:         null,
            source:         null,
            limit:          int.MaxValue,
            offset:         0,
            descending:     false,
            nativeOnly:     false,
            afterTimestamp: null,
            afterId:        null,
            ct:             ct);

        var stages    = session.Stages ?? [];
        var breakdown = SleepReportCalculator.ComputeStageBreakdown(session);
        var tir       = SleepReportCalculator.ComputeOvernightTir(session, glucoseReadings);
        var hypos     = SleepReportCalculator.ComputeHypoEvents(session, glucoseReadings, stages);
        var dawn      = SleepReportCalculator.ComputeDawnPhenomenon(session, glucoseReadings);
        var wakeEvents = SleepReportCalculator.ComputeWakeEvents(session, stages, glucoseReadings);
        var (score, scoreSource) = SleepReportCalculator.ResolveScore(session, hypos.Count, breakdown);

        return new SleepSingleNightReport
        {
            Session        = session,
            Score          = score,
            ScoreSource    = scoreSource ?? SleepScoreSource.Computed,
            StageBreakdown = breakdown,
            OvernightTir   = tir,
            HypoEvents     = hypos,
            DawnPhenomenon = dawn,
            WakeEvents     = wakeEvents,
        };
    }

    /// <inheritdoc/>
    public async Task<SleepTrendsReport> GetTrendsReportAsync(
        DateTime from,
        DateTime to,
        SleepSource? source = null,
        CancellationToken ct = default)
    {
        var allSessions = await _sessions.GetSessionsAsync(
            from:              from,
            to:                to,
            type:              null,
            source:            source,
            limit:             int.MaxValue,
            offset:            0,
            descending:        false,
            cancellationToken: ct);

        if (!allSessions.Any())
            return new SleepTrendsReport();

        IReadOnlyList<SleepSession> sessions = source is null
            ? SleepReportCalculator.DeduplicateToOnePerNight(allSessions)
            : (IReadOnlyList<SleepSession>)allSessions.ToList();

        var glucoseFrom = sessions.Min(s => s.StartTime);
        var glucoseTo   = sessions.Max(s => s.EndTime);

        var allGlucose = await _glucose.GetAsync(
            from:           glucoseFrom,
            to:             glucoseTo,
            device:         null,
            source:         null,
            limit:          int.MaxValue,
            offset:         0,
            descending:     false,
            nativeOnly:     false,
            afterTimestamp: null,
            afterId:        null,
            ct:             ct);

        var nights  = sessions.Select(s => SleepReportCalculator.ComputeNightSummary(s, allGlucose)).ToList();
        var summary = SleepReportCalculator.ComputeTrendsSummary(nights);

        return new SleepTrendsReport
        {
            Nights  = nights,
            Summary = summary,
        };
    }
}
