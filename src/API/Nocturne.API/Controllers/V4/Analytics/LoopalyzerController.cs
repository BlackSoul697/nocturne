using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Nocturne.Core.Contracts.Loopalyzer;
using Nocturne.Core.Models.Loopalyzer;
using OpenApi.Remote.Attributes;

namespace Nocturne.API.Controllers.V4.Analytics;

/// <summary>
/// Loopalyzer report data: per-day binned series across a tenant date range,
/// plus profile metadata for the active range and an availability probe used
/// by the reports menu to gate visibility for tenants without APS data.
/// </summary>
[ApiController]
[Route("api/v4/[controller]")]
[Produces("application/json")]
public class LoopalyzerController : ControllerBase
{
    private readonly ILoopalyzerService _service;

    public LoopalyzerController(ILoopalyzerService service)
    {
        _service = service;
    }

    /// <summary>
    /// Get Loopalyzer report data for a date range (max 14 days).
    /// </summary>
    [HttpGet]
    [RemoteQuery]
    [ProducesResponseType(typeof(LoopalyzerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoopalyzerResponse>> GetData(
        [FromQuery] string from,
        [FromQuery] string to,
        CancellationToken ct)
    {
        try
        {
            var response = await _service.GetDataAsync(new LoopalyzerRequest { From = from, To = to }, ct);
            return Ok(response);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Whether this tenant has any APS data in the last 30 days. Used to gate the
    /// Loopalyzer entry in the reports menu.
    /// </summary>
    [HttpGet("availability")]
    [RemoteQuery]
    [ProducesResponseType(typeof(LoopalyzerAvailability), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoopalyzerAvailability>> GetAvailability(CancellationToken ct)
        => Ok(await _service.GetAvailabilityAsync(ct));
}
