using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Attributes;
using Nocturne.API.Authorization;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.API.Extensions;
using Nocturne.API.Services.Analytics;
using Nocturne.Core.Clinical.LoopAnalysis;
using Nocturne.Core.Contracts.Analytics;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Authorization;
using Nocturne.Core.Models.LoopAnalysis;
using OpenApi.Remote.Attributes;

namespace Nocturne.API.Controllers.V4.Analytics;

/// <summary>
/// The Loop Analysis report: every tab is computed server-side from one fetch per request and
/// returned as <c>{ primary, comparison? }</c> inside the <see cref="LoopAnalysisResponse"/>
/// envelope.
/// </summary>
/// <remarks>
/// Every action takes the same query parameters, resolves them with <see cref="TryResolveAsync"/>
/// and fills the envelope with <see cref="LoopAnalysisRequest.Fill"/>. Responses carry a 60-second
/// client-only cache and nothing is cached server-side, because
/// <see cref="LoopAnalysisReadScopeGuard"/> narrows each response to its caller.
/// </remarks>
/// <seealso cref="ILoopAnalysisService"/>
[ApiController]
[Tags("Analytics")]
[Route("api/v4/loop-analysis")]
[Produces("application/json")]
public class LoopAnalysisController(
    ILoopAnalysisService service,
    TenantClockResolver tenantClock,
    TimeProvider timeProvider) : ControllerBase
{
    private static readonly DateOnly EarliestDate = new(1970, 1, 1);
    private static readonly DateOnly LatestDate = new(2999, 12, 31);

    /// <summary>
    /// Every series in 288 local five-minute slots of the day: glucose, scheduled and delivered
    /// basal, net loop correction and its components, IOB/COB, manual boluses, carbs and the
    /// target range, with the days behind them.
    /// </summary>
    /// <param name="startDate">First tenant-local calendar date of the range.</param>
    /// <param name="endDate">
    /// Last tenant-local calendar date of the range, inclusive. A range of more than 90 days keeps
    /// only its most recent 90 (<see cref="LoopAnalysisResolvedRange.Shortened"/>).
    /// </param>
    /// <param name="compare">Which comparison range to compute, if any.</param>
    /// <param name="compareStartDate">First local date of a custom comparison range.</param>
    /// <param name="compareEndDate">Last local date of a custom comparison range, inclusive, under the same 90-day limit.</param>
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
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] LoopAnalysisComparison compare = LoopAnalysisComparison.None,
        [FromQuery] DateOnly? compareStartDate = null,
        [FromQuery] DateOnly? compareEndDate = null,
        [FromQuery] DayOfWeek[]? days = null,
        [FromQuery] bool excludeOverrides = false,
        [FromQuery] bool excludeTemporaryTargets = false,
        [FromQuery] bool excludeExercise = false,
        [FromQuery] bool excludeIllness = true,
        [FromQuery] bool excludeOpenLoop = true,
        [FromQuery] string? profileName = null,
        CancellationToken cancellationToken = default)
    {
        var (invalid, request) = await TryResolveAsync(
            Query(startDate, endDate, compare, compareStartDate, compareEndDate, days, excludeOverrides,
                excludeTemporaryTargets, excludeExercise, excludeIllness, excludeOpenLoop, profileName),
            cancellationToken);
        if (invalid is not null)
            return invalid;

        var result = request!.Zone is { } zone
            ? await service.GetDailyProfileAsync(request.Filter!, zone, cancellationToken)
            : new DailyProfileResponse();
        return Ok(LoopAnalysisReadScopeGuard.Redact(request.Fill(result), HttpContext.GetGrantedScopes()));
    }

    /// <summary>
    /// How the loop ran, each figure for the range and by local hour of the day: closed-loop, pump
    /// and CGM time, gaps between loop cycles and in the pump's delivery records, zero delivery by
    /// origin, time at the effective maximum basal rate and which cap bound it, time at oref's
    /// <c>max_iob</c>, automatic boluses, suggestions not enacted and the sensitivity ratio.
    /// </summary>
    /// <inheritdoc cref="GetDailyProfile" path="/param"/>
    [HttpGet("loop-health")]
    [RemoteQuery]
    [RequireScope(LoopAnalysisReadScopeGuard.AdmissionScope)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(typeof(LoopHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoopHealthResponse>> GetLoopHealth(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] LoopAnalysisComparison compare = LoopAnalysisComparison.None,
        [FromQuery] DateOnly? compareStartDate = null,
        [FromQuery] DateOnly? compareEndDate = null,
        [FromQuery] DayOfWeek[]? days = null,
        [FromQuery] bool excludeOverrides = false,
        [FromQuery] bool excludeTemporaryTargets = false,
        [FromQuery] bool excludeExercise = false,
        [FromQuery] bool excludeIllness = true,
        [FromQuery] bool excludeOpenLoop = true,
        [FromQuery] string? profileName = null,
        CancellationToken cancellationToken = default)
    {
        var (invalid, request) = await TryResolveAsync(
            Query(startDate, endDate, compare, compareStartDate, compareEndDate, days, excludeOverrides,
                excludeTemporaryTargets, excludeExercise, excludeIllness, excludeOpenLoop, profileName),
            cancellationToken);
        if (invalid is not null)
            return invalid;

        var result = request!.Zone is { } zone
            ? await service.GetLoopHealthAsync(request.Filter!, zone, cancellationToken)
            : new LoopHealthResponse();
        return Ok(LoopAnalysisReadScopeGuard.Redact(request.Fill(result), HttpContext.GetGrantedScopes()));
    }

    private static LoopAnalysisQuery Query(
        DateOnly startDate,
        DateOnly endDate,
        LoopAnalysisComparison compare,
        DateOnly? compareStartDate,
        DateOnly? compareEndDate,
        DayOfWeek[]? days,
        bool excludeOverrides,
        bool excludeTemporaryTargets,
        bool excludeExercise,
        bool excludeIllness,
        bool excludeOpenLoop,
        string? profileName) =>
        new(startDate, endDate, compare, compareStartDate, compareEndDate,
            new LoopAnalysisMasks
            {
                Days = days is { Length: > 0 } ? [.. days.Distinct().Order()] : [.. Enum.GetValues<DayOfWeek>()],
                ExcludeOverrides = excludeOverrides,
                ExcludeTemporaryTargets = excludeTemporaryTargets,
                ExcludeExercise = excludeExercise,
                ExcludeIllness = excludeIllness,
                ExcludeOpenLoop = excludeOpenLoop,
                ProfileName = profileName,
            });

    /// <summary>The query parameters every Loop Analysis action takes, as bound.</summary>
    private sealed record LoopAnalysisQuery(
        DateOnly StartDate,
        DateOnly EndDate,
        LoopAnalysisComparison Compare,
        DateOnly? CompareStartDate,
        DateOnly? CompareEndDate,
        LoopAnalysisMasks Masks);

    /// <summary>A request resolved against the tenant clock and the caller.</summary>
    /// <param name="Filter">Null when <see cref="Zone"/> is: without a zone the dates cannot be placed.</param>
    private sealed record LoopAnalysisRequest(
        LoopAnalysisFilter? Filter,
        TimeZoneInfo? Zone,
        TimeZoneUnavailableReason? Reason,
        LoopAnalysisResolvedRange? PrimaryRange,
        LoopAnalysisResolvedRange? ComparisonRange,
        LoopAnalysisMasks Masks,
        List<string> IgnoredFilters,
        bool HistoryClamped)
    {
        public T Fill<T>(T response) where T : LoopAnalysisResponse
        {
            response.HistoryClamped = HistoryClamped;
            response.TimeZoneUnavailableReason = Reason;
            response.PrimaryRange = PrimaryRange;
            response.ComparisonRange = ComparisonRange;
            response.AppliedFilters = Masks;
            response.IgnoredFilters = IgnoredFilters;
            return response;
        }
    }

    /// <summary>
    /// Validates the query, drops the masks the caller may not use
    /// (<see cref="LoopAnalysisReadScopeGuard.NarrowFilter"/>) and resolves the local dates to
    /// instants in the tenant zone (<see cref="LoopAnalysisRanges"/>). A history-clamped caller is
    /// analysed over its last <see cref="HistoryClamp.Window"/> whatever dates it asked for, with
    /// no comparison. Every resolved range ends after it starts.
    /// </summary>
    private async Task<(ObjectResult? Invalid, LoopAnalysisRequest? Request)> TryResolveAsync(
        LoopAnalysisQuery query, CancellationToken ct)
    {
        if (OutOfRange(query.StartDate) || OutOfRange(query.EndDate))
            return (BadRange("startDate and endDate must be dates between 1970 and 2999."), null);
        if (query.EndDate < query.StartDate)
            return (BadRange("endDate must not be before startDate."), null);

        var custom = query.Compare == LoopAnalysisComparison.Custom;
        if (custom != (query.CompareStartDate.HasValue && query.CompareEndDate.HasValue)
            || (!custom && (query.CompareStartDate.HasValue || query.CompareEndDate.HasValue)))
            return (BadRange("compareStartDate and compareEndDate are given together, and only with compare=custom."), null);
        if (query is { CompareStartDate: { } compareStart, CompareEndDate: { } compareEnd })
        {
            if (OutOfRange(compareStart) || OutOfRange(compareEnd))
                return (BadRange("compareStartDate and compareEndDate must be dates between 1970 and 2999."), null);
            if (compareEnd < compareStart)
                return (BadRange("compareEndDate must not be before compareStartDate."), null);
        }

        var passed = Request.Query.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignored = LoopAnalysisReadScopeGuard.NarrowFilter(query.Masks, HttpContext.GetGrantedScopes());
        var clamped = HttpContext.IsCallerHistoryClamped();
        if (clamped)
            ignored.AddRange(["startDate", "endDate", "compare", "compareStartDate", "compareEndDate"]);
        ignored = [.. ignored.Where(passed.Contains)];

        var (zone, reason) = await tenantClock.ResolveAsync(ct);
        if (zone is null)
            return (null, new LoopAnalysisRequest(null, null, reason, null, null, query.Masks, ignored, clamped));

        var maxDays = V4ReadLimits.MaxAnalyticsSpanDays;
        var primary = clamped
            ? LoopAnalysisRanges.Clamped(timeProvider.GetUtcNow().UtcDateTime, HistoryClamp.Window, zone)
            : LoopAnalysisRanges.Resolve(query.StartDate, query.EndDate, zone, maxDays);
        var comparison = clamped ? null : query.Compare switch
        {
            LoopAnalysisComparison.Previous => LoopAnalysisRanges.Previous(primary, zone),
            LoopAnalysisComparison.Custom => LoopAnalysisRanges.Resolve(
                query.CompareStartDate!.Value, query.CompareEndDate!.Value, zone, maxDays),
            _ => null,
        };

        var filter = new LoopAnalysisFilter
        {
            From = primary.From,
            To = primary.To,
            CompareFrom = comparison?.From,
            CompareTo = comparison?.To,
            Masks = query.Masks,
        };
        return (null, new LoopAnalysisRequest(filter, zone, null, primary, comparison, query.Masks, ignored, clamped));
    }

    private static bool OutOfRange(DateOnly date) => date < EarliestDate || date > LatestDate;

    private ObjectResult BadRange(string detail) => Problem(detail: detail, statusCode: 400, title: "Bad Request");
}
