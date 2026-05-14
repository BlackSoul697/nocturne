using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.Core.Contracts.Monitoring;

namespace Nocturne.API.Controllers.V4.Monitoring;

/// <summary>
/// Scaffolds tracker definitions and alarm rules from consumable catalog templates.
/// </summary>
/// <seealso cref="ITrackerTemplateService"/>
[ApiController]
[Tags("Monitoring")]
[Route("api/v4/trackers/templates")]
public class TrackerTemplatesController : ControllerBase
{
    private readonly ITrackerTemplateService _templateService;

    /// <summary>
    /// Initializes a new instance of <see cref="TrackerTemplatesController"/>.
    /// </summary>
    /// <param name="templateService">Service for discovering and applying consumable templates.</param>
    public TrackerTemplatesController(ITrackerTemplateService templateService)
    {
        _templateService = templateService;
    }

    /// <summary>
    /// Get available templates based on the user's registered patient devices.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [RemoteQuery]
    [ProducesResponseType(typeof(AvailableTemplate[]), StatusCodes.Status200OK)]
    public async Task<ActionResult<AvailableTemplate[]>> GetTemplates()
    {
        var templates = await _templateService.GetAvailableTemplatesAsync(
            HttpContext.RequestAborted
        );

        return Ok(templates);
    }

    /// <summary>
    /// Apply a template, creating a tracker definition and alarm rules.
    /// </summary>
    [HttpPost("apply")]
    [Authorize]
    [RemoteCommand(Invalidates = ["GetDefinitions", "GetAlertRules"])]
    [ProducesResponseType(typeof(TemplateResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<TemplateResult>> ApplyTemplate(
        [FromBody] TemplateApplication request
    )
    {
        var result = await _templateService.ApplyTemplateAsync(
            request,
            HttpContext.RequestAborted
        );

        return Ok(result);
    }
}
