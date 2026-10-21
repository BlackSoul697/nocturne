using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Attributes;
using Nocturne.API.Authorization;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.API.Extensions;
using Nocturne.Core.Contracts.Analytics;
using Nocturne.Core.Models.Authorization;
using Nocturne.Core.Models.LoopAnalysis;
using OpenApi.Remote.Attributes;

namespace Nocturne.API.Controllers.V4.Analytics;

/// <summary>
/// The Loop Analysis report: every tab is computed server-side from one fetch per request and
/// returned as <c>{ primary, comparison? }</c> for the ranges its <see cref="LoopAnalysisFilter"/> names.
/// </summary>
/// <remarks>
/// Every action takes the filter as the same query parameters and builds it with
/// <see cref="TryBuildFilter"/>. Responses carry a 60-second client-only cache and nothing is
/// cached server-side, because <see cref="LoopAnalysisReadScopeGuard"/> narrows each response to
/// its caller.
/// </remarks>
/// <seealso cref="ILoopAnalysisService"/>
[ApiController]
[Tags("Analytics")]
[Route("api/v4/loop-analysis")]
[Produces("application/json")]
public class LoopAnalysisController(ILoopAnalysisService service, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Every series in 288 local five-minute slots of the day: glucose, scheduled and delivered
    /// basal, net loop correction and its components, IOB/COB, manual boluses, carbs and the
    /// target range, with the days behind them.
    /// </summary>
    /// <param name="from">Start of the range (inclusive, UTC).</param>
    /// <param name="to">
    /// End of the range (exclusive, UTC), at most 90 days of elapsed time after
    /// <paramref name="from"/>. A range of 90 local days that crosses a fall-back transition is 90
    /// days and an hour long, and is rejected.
    /// </param>
    /// <param name="compareFrom">Start of the comparison range; given together with <paramref name="compareTo"/>.</param>
    /// <param name="compareTo">End of the comparison range, under the same limit as <paramref name="to"/>.</param>
    /// <param name="days">Local weekdays to analyse; every day when empty.</param>
    /// <param name="excludeOverrides">Mask override time; ignored without treatments.read.</param>
    /// <param name="excludeTemporaryTargets">Mask temporary target time; ignored without treatments.read.</param>
    /// <param name="excludeExercise">Mask exercise time; ignored without treatments.read.</param>
    /// <param name="excludeIllness">Mask illness time; ignored without treatments.read. On by default.</param>
    /// <param name="excludeOpenLoop">Mask time the pump was in manual mode or had no AID algorithm; ignored without devices.read. On by default.</param>
    /// <param name="profileName">Analyse only time this profile was active; ignored without therapy.read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("daily-profile")]
    [RemoteQuery]
    [RequireScope(LoopAnalysisReadScopeGuard.AdmissionScope)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(typeof(DailyProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DailyProfileResponse>> GetDailyProfile(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] DateTime? compareFrom = null,
        [FromQuery] DateTime? compareTo = null,
        [FromQuery] DayOfWeek[]? days = null,
        [FromQuery] bool excludeOverrides = false,
        [FromQuery] bool excludeTemporaryTargets = false,
        [FromQuery] bool excludeExercise = false,
        [FromQuery] bool excludeIllness = true,
        [FromQuery] bool excludeOpenLoop = true,
        [FromQuery] string? profileName = null,
        CancellationToken cancellationToken = default)
    {
        if (TryBuildFilter(
                FilterOf(from, to, compareFrom, compareTo, days, excludeOverrides, excludeTemporaryTargets,
                    excludeExercise, excludeIllness, excludeOpenLoop, profileName),
                out var request) is { } invalid)
            return invalid;

        var result = await service.GetDailyProfileAsync(request.Filter, cancellationToken);
        result.HistoryClamped = request.HistoryClamped;
        result.IgnoredFilters = request.IgnoredFilters;
        return Ok(LoopAnalysisReadScopeGuard.Redact(result, HttpContext.GetGrantedScopes()));
    }

    /// <summary>
    /// How far each prediction curve's values at +30, +60, +90 and +120 minutes landed from the
    /// sensor reading nearest the time they predicted, overall, by local hour of day and by context.
    /// </summary>
    /// <remarks>
    /// Only the +30 and +60 minute bias is a sign of the settings a prediction ran on: the reading at
    /// a longer horizon follows everything the loop did after the prediction, which the prediction
    /// assumed would not happen. Error is predicted minus actual, so a positive bias is a prediction
    /// that ran high. A horizon with no reading within 2.5 minutes is counted as unmatched, never
    /// paired with an imputed value. Needs glucose.read and devices.read for any curve, the
    /// readings and the APS snapshots the curves come from; without treatments.read the context
    /// breakdowns are withheld, and so are oref's IOB, zero-temp and COB curves, each reported only
    /// in the contexts the algorithm relies on it in.
    /// </remarks>
    /// <param name="from">Start of the range (inclusive, UTC).</param>
    /// <param name="to">
    /// End of the range (exclusive, UTC), at most 90 days of elapsed time after
    /// <paramref name="from"/>. A range of 90 local days that crosses a fall-back transition is 90
    /// days and an hour long, and is rejected.
    /// </param>
    /// <param name="compareFrom">Start of the comparison range; given together with <paramref name="compareTo"/>.</param>
    /// <param name="compareTo">End of the comparison range, under the same limit as <paramref name="to"/>.</param>
    /// <param name="days">Local weekdays to analyse; every day when empty.</param>
    /// <param name="excludeOverrides">Mask override time; ignored without treatments.read.</param>
    /// <param name="excludeTemporaryTargets">Mask temporary target time; ignored without treatments.read.</param>
    /// <param name="excludeExercise">Mask exercise time; ignored without treatments.read.</param>
    /// <param name="excludeIllness">Mask illness time; ignored without treatments.read. On by default.</param>
    /// <param name="excludeOpenLoop">Mask time the pump was in manual mode or had no AID algorithm; ignored without devices.read. On by default.</param>
    /// <param name="profileName">Analyse only time this profile was active; ignored without therapy.read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("prediction-accuracy")]
    [RemoteQuery]
    [RequireScope(LoopAnalysisReadScopeGuard.AdmissionScope)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(typeof(PredictionAccuracyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PredictionAccuracyResponse>> GetPredictionAccuracy(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] DateTime? compareFrom = null,
        [FromQuery] DateTime? compareTo = null,
        [FromQuery] DayOfWeek[]? days = null,
        [FromQuery] bool excludeOverrides = false,
        [FromQuery] bool excludeTemporaryTargets = false,
        [FromQuery] bool excludeExercise = false,
        [FromQuery] bool excludeIllness = true,
        [FromQuery] bool excludeOpenLoop = true,
        [FromQuery] string? profileName = null,
        CancellationToken cancellationToken = default)
    {
        if (TryBuildFilter(
                FilterOf(from, to, compareFrom, compareTo, days, excludeOverrides, excludeTemporaryTargets,
                    excludeExercise, excludeIllness, excludeOpenLoop, profileName),
                out var request) is { } invalid)
            return invalid;

        var result = await service.GetPredictionAccuracyAsync(request.Filter, cancellationToken);
        result.HistoryClamped = request.HistoryClamped;
        result.IgnoredFilters = request.IgnoredFilters;
        return Ok(LoopAnalysisReadScopeGuard.Redact(result, HttpContext.GetGrantedScopes()));
    }

    private static LoopAnalysisFilter FilterOf(
        DateTime from, DateTime to, DateTime? compareFrom, DateTime? compareTo, DayOfWeek[]? days,
        bool excludeOverrides, bool excludeTemporaryTargets, bool excludeExercise, bool excludeIllness,
        bool excludeOpenLoop, string? profileName) => new()
    {
        From = from,
        To = to,
        CompareFrom = compareFrom,
        CompareTo = compareTo,
        Days = days?.ToList(),
        ExcludeOverrides = excludeOverrides,
        ExcludeTemporaryTargets = excludeTemporaryTargets,
        ExcludeExercise = excludeExercise,
        ExcludeIllness = excludeIllness,
        ExcludeOpenLoop = excludeOpenLoop,
        ProfileName = profileName,
    };

    private sealed record FilterRequest(LoopAnalysisFilter Filter, List<string> IgnoredFilters, bool HistoryClamped);

    /// <summary>
    /// Validates <paramref name="filter"/>, drops the parts the caller may not use
    /// (<see cref="LoopAnalysisReadScopeGuard.NarrowFilter"/>) and starts each range no earlier
    /// than a history-clamped caller can read.
    /// </summary>
    /// <returns>The error response, or null with <paramref name="request"/> set.</returns>
    private ObjectResult? TryBuildFilter(LoopAnalysisFilter filter, out FilterRequest request)
    {
        request = null!;
        filter.From = AsUtc(filter.From);
        filter.To = AsUtc(filter.To);
        filter.CompareFrom = filter.CompareFrom is { } compareFrom ? AsUtc(compareFrom) : null;
        filter.CompareTo = filter.CompareTo is { } compareTo ? AsUtc(compareTo) : null;

        if (filter.To <= filter.From)
            return BadRange("to must be after from.");
        if (this.RejectDateSpan(filter.From, filter.To, V4ReadLimits.MaxAnalyticsSpanDays) is { } overlong)
            return overlong;

        if (filter.CompareFrom.HasValue != filter.CompareTo.HasValue)
            return BadRange("compareFrom and compareTo are given together.");
        if (filter.CompareFrom is { } start && filter.CompareTo is { } end)
        {
            if (end <= start)
                return BadRange("compareTo must be after compareFrom.");
            if (this.RejectDateSpan(start, end, V4ReadLimits.MaxAnalyticsSpanDays) is { } overlongComparison)
                return overlongComparison;
        }

        var passed = Request.Query.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignored = LoopAnalysisReadScopeGuard.NarrowFilter(filter, HttpContext.GetGrantedScopes())
            .Where(passed.Contains)
            .ToList();

        var clamped = HttpContext.IsCallerHistoryClamped();
        if (clamped)
        {
            var floor = timeProvider.GetUtcNow().UtcDateTime - HistoryClamp.Window;
            filter.From = ClampStart(filter.From, filter.To, floor);
            if (filter.CompareFrom is { } clampedStart && filter.CompareTo is { } clampedEnd)
                filter.CompareFrom = ClampStart(clampedStart, clampedEnd, floor);
        }

        request = new FilterRequest(filter, ignored, clamped);
        return null;
    }

    /// <summary>
    /// The later of <paramref name="start"/> and <paramref name="floor"/>, but never past
    /// <paramref name="end"/>: a range wholly before the floor becomes empty at its end.
    /// </summary>
    private static DateTime ClampStart(DateTime start, DateTime end, DateTime floor) =>
        start >= floor ? start : floor <= end ? floor : end;

    private ObjectResult BadRange(string detail) => Problem(detail: detail, statusCode: 400, title: "Bad Request");

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
