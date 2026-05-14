using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Services.Monitoring;
using Nocturne.Core.Models;

namespace Nocturne.API.Controllers.V4.Monitoring;

/// <summary>
/// Controller for tracker alert management.
/// </summary>
/// <seealso cref="ITrackerAlertService"/>
[ApiController]
[Tags("Monitoring")]
[Authorize]
[Route("api/v4/trackers/alerts")]
public class TrackerAlertsController : ControllerBase
{
    private readonly ITrackerAlertService _alertService;
    private readonly ILogger<TrackerAlertsController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="TrackerAlertsController"/>.
    /// </summary>
    /// <param name="alertService">Service for querying tracker alerts.</param>
    /// <param name="logger">Logger instance.</param>
    public TrackerAlertsController(
        ITrackerAlertService alertService,
        ILogger<TrackerAlertsController> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    /// <inheritdoc cref="ITrackerAlertService.GetPendingAlertsAsync"/>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(TrackerAlertDto[]), 200)]
    public async Task<ActionResult<TrackerAlertDto[]>> GetPendingAlerts()
    {
        var alerts = await _alertService.GetPendingAlertsAsync(HttpContext.RequestAborted);

        return Ok(alerts.Select(a => new TrackerAlertDto
        {
            InstanceId = a.InstanceId,
            DefinitionId = a.DefinitionId,
            ThresholdId = a.ThresholdId,
            TrackerName = a.TrackerName,
            Urgency = a.Urgency,
            Message = a.Message,
        }).ToArray());
    }
}

/// <summary>
/// DTO for tracker alerts returned to the frontend
/// </summary>
public class TrackerAlertDto
{
    public Guid InstanceId { get; set; }
    public Guid DefinitionId { get; set; }
    public Guid ThresholdId { get; set; }
    public string TrackerName { get; set; } = string.Empty;
    public NotificationUrgency Urgency { get; set; }
    public string Message { get; set; } = string.Empty;
}
