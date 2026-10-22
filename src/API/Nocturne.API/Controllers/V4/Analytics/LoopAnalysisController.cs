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
/// Every action binds a <see cref="LoopAnalysisQuery"/>, resolves it with <see cref="TryResolveAsync"/>
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
    /// <param name="query">The range, comparison and masks (<see cref="LoopAnalysisQuery"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("daily-profile")]
    [RemoteQuery]
    [RequireScope(LoopAnalysisReadScopeGuard.AdmissionScope)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(typeof(DailyProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<DailyProfileResponse>> GetDailyProfile(
        [FromQuery] LoopAnalysisQuery query,
        CancellationToken cancellationToken = default) =>
        ServeAsync(query, service.GetDailyProfileAsync, cancellationToken);

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
    /// <param name="query">The range, comparison and masks (<see cref="LoopAnalysisQuery"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("prediction-accuracy")]
    [RemoteQuery]
    [RequireScope(LoopAnalysisReadScopeGuard.AdmissionScope)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(typeof(PredictionAccuracyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<PredictionAccuracyResponse>> GetPredictionAccuracy(
        [FromQuery] LoopAnalysisQuery query,
        CancellationToken cancellationToken = default) =>
        ServeAsync(query, service.GetPredictionAccuracyAsync, cancellationToken);

    /// <summary>
    /// Resolves <paramref name="query"/>, computes the tab when the tenant has a zone, fills the
    /// envelope and narrows the result to the caller.
    /// </summary>
    private async Task<ActionResult<T>> ServeAsync<T>(
        LoopAnalysisQuery query,
        Func<LoopAnalysisFilter, TimeZoneInfo, CancellationToken, Task<T>> compute,
        CancellationToken ct)
        where T : LoopAnalysisResponse, new()
    {
        var (invalid, request) = await TryResolveAsync(query, ct);
        if (invalid is not null)
            return invalid;

        var result = request!.Zone is { } zone ? await compute(request.Filter!, zone, ct) : new T();
        return Ok(LoopAnalysisReadScopeGuard.Redact(request.Fill(result), HttpContext.GetGrantedScopes()));
    }

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

        var masks = query.ToMasks();
        var passed = Request.Query.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignored = LoopAnalysisReadScopeGuard.NarrowFilter(masks, HttpContext.GetGrantedScopes());
        var clamped = HttpContext.IsCallerHistoryClamped();
        if (clamped)
            ignored.AddRange(["startDate", "endDate", "compare", "compareStartDate", "compareEndDate"]);
        ignored = [.. ignored.Where(passed.Contains)];

        var (zone, reason) = await tenantClock.ResolveAsync(ct);
        if (zone is null)
            return (null, new LoopAnalysisRequest(null, null, reason, null, null, masks, ignored, clamped));

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
            Masks = masks,
        };
        return (null, new LoopAnalysisRequest(filter, zone, null, primary, comparison, masks, ignored, clamped));
    }

    private static bool OutOfRange(DateOnly date) => date < EarliestDate || date > LatestDate;

    private ObjectResult BadRange(string detail) => Problem(detail: detail, statusCode: 400, title: "Bad Request");
}
