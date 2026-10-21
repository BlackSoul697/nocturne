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
/// returned as <c>{ primary, comparison? }</c> for the ranges a <see cref="LoopAnalysisFilter"/> names.
/// </summary>
/// <remarks>
/// Responses carry a 60-second client-only cache and nothing is cached server-side, because
/// <see cref="LoopAnalysisReadScopeGuard"/> narrows each response to its caller.
/// </remarks>
/// <seealso cref="ILoopAnalysisService"/>
[ApiController]
[Tags("Analytics")]
[Route("api/v4/loop-analysis")]
[Produces("application/json")]
public class LoopAnalysisController(ILoopAnalysisService service) : ControllerBase
{
    /// <summary>
    /// Every series in 288 local five-minute slots of the day: glucose, scheduled and delivered
    /// basal, net loop correction and its components, IOB/COB, manual boluses, carbs and the
    /// target range, with the days behind them.
    /// </summary>
    [HttpGet("daily-profile")]
    [RemoteQuery]
    [RequireScope(LoopAnalysisReadScopeGuard.AdmissionScope)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [ProducesResponseType(typeof(DailyProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DailyProfileResponse>> GetDailyProfile(
        [FromQuery] LoopAnalysisFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (Reject(filter) is { } invalid)
            return invalid;

        var result = await service.GetDailyProfileAsync(filter, cancellationToken);
        result.HistoryClamped = HttpContext.IsCallerHistoryClamped();
        return Ok(LoopAnalysisReadScopeGuard.Redact(result, HttpContext.GetGrantedScopes()));
    }

    /// <summary>
    /// Normalises the filter's bounds to UTC and rejects a range that is empty, inverted or longer
    /// than <see cref="V4ReadLimits.MaxAnalyticsSpanDays"/>, and a comparison given by one bound.
    /// </summary>
    private ObjectResult? Reject(LoopAnalysisFilter filter)
    {
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

        return null;
    }

    private ObjectResult BadRange(string detail) => Problem(detail: detail, statusCode: 400, title: "Bad Request");

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
