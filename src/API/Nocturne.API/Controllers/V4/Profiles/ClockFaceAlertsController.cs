using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenApi.Remote.Attributes;
using Nocturne.API.Extensions;
using Nocturne.API.Services.Alerts;
using Nocturne.Core.Contracts.Platform;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;
using Nocturne.Infrastructure.Data.Services;

namespace Nocturne.API.Controllers.V4.Profiles;

/// <summary>
/// The alarms a public clock face shows, for a face whose owner turned on
/// <see cref="ClockSettings.ShowAlerts"/>.
/// </summary>
/// <remarks>
/// The clock id is the capability, as for <see cref="ClockFacesController.GetGlucose"/>: a caller
/// holding a valid id for the host's tenant gets what the face is set to show, read under tenant
/// RLS as a reader who is not a share.
/// </remarks>
[ApiController]
[Tags("Profiles")]
[Route("api/v4/clockfaces/{id:guid}/alerts")]
[AllowAnonymous]
[EnableRateLimiting(ServiceRegistrationExtensions.NoAccountAlertsRateLimitPolicy)]
public class ClockFaceAlertsController(
    IClockFaceService clockFaces,
    INocturneDbContextFactory contexts,
    AlertReceiptService receipts) : ControllerBase
{
    /// <summary>The alarms firing now, or none when the face does not show alerts.</summary>
    [HttpGet]
    [RemoteQuery]
    [ProducesResponseType(typeof(ClockAlertDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClockAlertDto[]>> GetClockAlerts(Guid id)
    {
        var ct = HttpContext.RequestAborted;
        var face = await clockFaces.GetByIdAsync(id, ct);
        if (face is null)
            return NotFound();

        if (!face.Config.Settings.ShowAlerts)
            return Ok(Array.Empty<ClockAlertDto>());

        await using var db = await contexts.CreateAsync(ct);
        var now = DateTime.UtcNow;
        var excursions = await db.AlertExcursions
            .AsNoTracking()
            .Include(e => e.AlertRule)
            .Include(e => e.Instances)
            .Where(e => e.EndedAt == null)
            .OrderByDescending(e => e.StartedAt)
            .ToListAsync(ct);
        var seen = await AlertReceiptService.ReadAsync(
            db, excursions.Select(e => e.Id).ToList(), Actor(face), ct);

        return Ok(excursions.Select(e => new ClockAlertDto
        {
            Id = e.Id,
            RuleName = e.AlertRule?.Name ?? string.Empty,
            Severity = e.AlertRule?.Severity ?? AlertRuleSeverity.Warning,
            ConditionType = e.AlertRule?.ConditionType ?? AlertConditionType.Threshold,
            StartedAt = e.StartedAt,
            Acknowledged = e.AcknowledgedAt is not null,
            SeenHere = seen[e.Id].Any(),
            SnoozedUntil = e.Instances
                .Where(i => i.ResolvedAt == null)
                .Select(i => AlertSnooze.ActiveUntil(i.SnoozedUntil, now))
                .Max(),
        }).ToArray());
    }

    /// <summary>
    /// Record that someone at the clock saw an alarm that is firing, for a face whose owner set
    /// <see cref="ClockSettings.AllowAcknowledge"/>. It acknowledges and mutes nothing; members see
    /// that the face saw it.
    /// </summary>
    [HttpPost("{excursionId:guid}/seen")]
    [RemoteCommand(Invalidates = [nameof(GetClockAlerts)])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MarkClockAlertSeen(Guid id, Guid excursionId)
    {
        var ct = HttpContext.RequestAborted;
        var face = await clockFaces.GetByIdAsync(id, ct);
        if (face is null || !face.Config.Settings.ShowAlerts || !face.Config.Settings.AllowAcknowledge)
            return NotFound();

        return await receipts.RecordAsync(excursionId, Actor(face), ct) ? NoContent() : NotFound();
    }

    private static AlertReceiptActor Actor(ClockFace face) =>
        new(AlertReceiptActorKind.Clock, face.Id, face.Name);
}
