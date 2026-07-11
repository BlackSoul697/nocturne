using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.Profiles.Resolvers;
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
/// <remarks>
/// Glycemic thresholds mirror <c>ProfileLoadStage</c>: very-low (54 mg/dL) and
/// very-high (250 mg/dL) are fixed; low/target-bottom and high/target-top come from
/// the active profile's target range, with 70/180 fallbacks when no therapy
/// settings exist.
/// </remarks>
public class SleepReportService : ISleepReportService
{
    private const double DefaultVeryLow  = 54;
    private const double DefaultLow      = 70;
    private const double DefaultHigh     = 180;
    private const double DefaultVeryHigh = 250;

    private readonly ISleepSessionRepository _sessions;
    private readonly ISensorGlucoseRepository _glucose;
    private readonly ITherapySettingsResolver _therapySettingsResolver;
    private readonly ITargetRangeResolver _targetRangeResolver;
    private readonly ILogger<SleepReportService> _logger;

    public SleepReportService(
        ISleepSessionRepository sessions,
        ISensorGlucoseRepository glucose,
        ITherapySettingsResolver therapySettingsResolver,
        ITargetRangeResolver targetRangeResolver,
        ILogger<SleepReportService> logger)
    {
        _sessions = sessions;
        _glucose  = glucose;
        _therapySettingsResolver = therapySettingsResolver;
        _targetRangeResolver     = targetRangeResolver;
        _logger   = logger;
    }

    /// <summary>
    /// Resolves glycemic thresholds at <paramref name="timeMills"/> the same way
    /// <c>ProfileLoadStage</c> does: very-low/very-high are fixed; low and high come
    /// from the active profile's target range, falling back to 70/180 when no therapy
    /// settings exist for the tenant.
    /// </summary>
    private async Task<GlycemicThresholds> ResolveThresholdsAsync(long timeMills, CancellationToken ct)
    {
        if (!await _therapySettingsResolver.HasDataAsync(ct))
        {
            return new GlycemicThresholds
            {
                VeryLow      = DefaultVeryLow,
                Low          = DefaultLow,
                TargetBottom = DefaultLow,
                High         = DefaultHigh,
                TargetTop    = DefaultHigh,
                VeryHigh     = DefaultVeryHigh,
            };
        }

        var low  = await _targetRangeResolver.GetLowBGTargetAsync(timeMills, ct: ct);
        var high = await _targetRangeResolver.GetHighBGTargetAsync(timeMills, ct: ct);
        return new GlycemicThresholds
        {
            VeryLow      = DefaultVeryLow,
            Low          = low,
            TargetBottom = low,
            High         = high,
            TargetTop    = high,
            VeryHigh     = DefaultVeryHigh,
        };
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

        var thresholds = await ResolveThresholdsAsync(session.EndMills, ct);
        var stages    = session.Stages ?? [];
        var breakdown = SleepReportCalculator.ComputeStageBreakdown(session);
        var tir       = SleepReportCalculator.ComputeOvernightTir(session, glucoseReadings, thresholds);
        var hypos     = SleepReportCalculator.ComputeHypoEvents(session, glucoseReadings, stages, thresholds);
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

        var daysInRange = (int)(to.Date - from.Date).TotalDays + 1;

        if (!allSessions.Any())
            return new SleepTrendsReport
            {
                Summary = SleepReportCalculator.ComputeTrendsSummary([], daysInRange),
            };

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

        // Slice the (date-range-bounded) glucose set per night so each night's
        // computation scans only its own window, not every reading in the range.
        var thresholds = await ResolveThresholdsAsync(new DateTimeOffset(glucoseTo, TimeSpan.Zero).ToUnixTimeMilliseconds(), ct);
        var nights = sessions
            .Select(s =>
            {
                var nightGlucose = allGlucose
                    .Where(g => g.Timestamp >= s.StartTime && g.Timestamp <= s.EndTime)
                    .ToList();
                return SleepReportCalculator.ComputeNightSummary(s, nightGlucose, thresholds);
            })
            .ToList();
        var summary = SleepReportCalculator.ComputeTrendsSummary(nights, daysInRange);

        return new SleepTrendsReport
        {
            Nights  = nights,
            Summary = summary,
        };
    }
}
