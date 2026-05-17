using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.Core.Contracts.Sleep;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Sleep.Report;
using OpenApi.Remote.Attributes;

namespace Nocturne.API.Controllers.V4.Sleep;

/// <summary>
/// On-demand sleep report endpoints: single-night detail and multi-night trends.
/// Both endpoints join CGM data with the session window at request time.
/// </summary>
/// <seealso cref="ISleepReportService"/>
[ApiController]
[Tags("Sleep")]
[Route("api/v4/sleep/report")]
[Authorize]
public class SleepReportController : ControllerBase
{
    private readonly ISleepReportService _service;

    public SleepReportController(ISleepReportService service)
    {
        _service = service;
    }

    /// <summary>
    /// Get the full single-night report for a sleep session, including stage breakdown,
    /// overnight TIR, hypo events, dawn phenomenon, and wake events.
    /// </summary>
    /// <param name="sessionId">The sleep session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("single-night/{sessionId:guid}")]
    [RemoteQuery]
    [ProducesResponseType(typeof(SleepSingleNightReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SleepSingleNightReport>> GetSingleNight(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var report = await _service.GetSingleNightReportAsync(sessionId, cancellationToken);
        if (report is null) return NotFound();
        return Ok(report);
    }

    /// <summary>
    /// Get a multi-night trends report. Maximum date range is 90 days.
    /// When <paramref name="source"/> is omitted, sessions are deduplicated to one per
    /// calendar night (longest sleep wins; source priority as tie-breaker).
    /// </summary>
    /// <param name="from">Start of the date range (inclusive).</param>
    /// <param name="to">End of the date range (inclusive).</param>
    /// <param name="source">Optional source filter. When provided, deduplication is skipped.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("trends")]
    [RemoteQuery]
    [ProducesResponseType(typeof(SleepTrendsReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SleepTrendsReport>> GetTrends(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] SleepSource? source = null,
        CancellationToken cancellationToken = default)
    {
        if ((to - from).TotalDays > 90)
            return Problem(
                detail: "Date range must not exceed 90 days.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");

        var report = await _service.GetTrendsReportAsync(from, to, source, cancellationToken);
        return Ok(report);
    }
}
