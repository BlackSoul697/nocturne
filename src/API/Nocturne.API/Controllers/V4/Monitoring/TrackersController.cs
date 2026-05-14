using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.API.Extensions;
using Nocturne.API.Services.Alerts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Abstractions;
using Nocturne.API.Services.Realtime;

namespace Nocturne.API.Controllers.V4.Monitoring;

/// <summary>
/// Controller for flexible tracker management (consumables, appointments, reminders).
/// </summary>
/// <seealso cref="ITrackerRepository"/>
/// <seealso cref="ISignalRBroadcastService"/>
[ApiController]
[Tags("Monitoring")]
[Route("api/v4/trackers")]
public class TrackersController : ControllerBase
{
    private readonly ITrackerRepository _repository;
    private readonly ISignalRBroadcastService _broadcast;
    private readonly IAlertReferenceService _referenceService;
    private readonly ILogger<TrackersController> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="TrackersController"/>.
    /// </summary>
    /// <param name="repository">Repository for tracker definition and log persistence.</param>
    /// <param name="broadcast">Service for broadcasting real-time tracker updates via SignalR.</param>
    /// <param name="referenceService">Cross-rule reference scanner for deletion guards.</param>
    /// <param name="logger">Logger instance.</param>
    public TrackersController(
        ITrackerRepository repository,
        ISignalRBroadcastService broadcast,
        IAlertReferenceService referenceService,
        ILogger<TrackersController> logger
    )
    {
        _repository = repository;
        _broadcast = broadcast;
        _referenceService = referenceService;
        _logger = logger;
    }

    #region Helpers

    /// <summary>
    /// Validate notification thresholds for a definition
    /// </summary>
    private static string? ValidateThresholds(
        List<CreateNotificationThresholdRequest>? thresholds,
        int? lifespanHours,
        TrackerMode mode)
    {
        if (thresholds == null) return null;

        foreach (var threshold in thresholds)
        {
            // For Duration mode, negative thresholds must not exceed lifespan
            if (mode == TrackerMode.Duration && threshold.Hours < 0)
            {
                if (!lifespanHours.HasValue)
                {
                    return "Negative thresholds require a lifespan to be set";
                }
                if (Math.Abs(threshold.Hours) >= lifespanHours.Value)
                {
                    return $"Negative threshold {threshold.Hours} exceeds tracker lifespan of {lifespanHours} hours";
                }
            }
        }

        return null;
    }

    #endregion

    #region Definitions

    /// <summary>
    /// Get all tracker definitions. Tenant-scoped via RLS.
    /// </summary>
    [HttpGet("definitions")]
    [AllowAnonymous]
    [RemoteQuery]
    [ProducesResponseType(typeof(TrackerDefinitionDto[]), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackerDefinitionDto[]>> GetDefinitions(
        [FromQuery] TrackerCategory? category = null
    )
    {
        List<TrackerDefinitionEntity> definitions;

        if (category.HasValue)
        {
            definitions = await _repository.GetDefinitionsByCategoryAsync(
                category.Value,
                HttpContext.RequestAborted
            );
        }
        else
        {
            definitions = await _repository.GetDefinitionsAsync(HttpContext.RequestAborted);
        }

        return Ok(definitions.Select(TrackerDefinitionDto.FromEntity).ToArray());
    }

    /// <summary>
    /// Get a specific tracker definition
    /// </summary>
    [HttpGet("definitions/{id:guid}")]
    [AllowAnonymous]
    [RemoteQuery]
    [ProducesResponseType(typeof(TrackerDefinitionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackerDefinitionDto>> GetDefinition(Guid id)
    {
        var definition = await _repository.GetDefinitionByIdAsync(id, HttpContext.RequestAborted);

        if (definition == null)
            return NotFound();

        return Ok(TrackerDefinitionDto.FromEntity(definition));
    }

    /// <summary>
    /// Create a new tracker definition
    /// </summary>
    [HttpPost("definitions")]
    [Authorize]
    [RemoteForm(Invalidates = ["GetDefinitions"])]
    [ProducesResponseType(typeof(TrackerDefinitionDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TrackerDefinitionDto>> CreateDefinition(
        [FromBody] CreateTrackerDefinitionRequest request
    )
    {
        // Validate thresholds
        var validationError = ValidateThresholds(
            request.NotificationThresholds,
            request.LifespanHours,
            request.Mode);
        if (validationError != null)
            return BadRequest(validationError);

        // Validate mode-specific requirements
        if (request.Mode == TrackerMode.Event && request.LifespanHours.HasValue)
            return Problem(detail: "Event mode trackers should not have a lifespan", statusCode: 400, title: "Bad Request");

        var entity = new TrackerDefinitionEntity
        {
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            Icon = request.Icon ?? "activity",
            TriggerEventTypes = JsonSerializer.Serialize(request.TriggerEventTypes ?? []),
            TriggerNotesContains = request.TriggerNotesContains,
            LifespanHours = request.LifespanHours,
            IsFavorite = request.IsFavorite,
            DashboardVisibility = request.DashboardVisibility,
            Visibility = request.Visibility,
            StartEventType = request.StartEventType,
            CompletionEventType = request.CompletionEventType,
            Mode = request.Mode,
        };

        // Add notification thresholds if provided
        if (request.NotificationThresholds != null)
        {
            foreach (var threshold in request.NotificationThresholds)
            {
                entity.NotificationThresholds.Add(
                    new TrackerNotificationThresholdEntity
                    {
                        Urgency = threshold.Urgency,
                        Hours = threshold.Hours,
                        Description = threshold.Description,
                        DisplayOrder = threshold.DisplayOrder,
                    }
                );
            }
        }

        var created = await _repository.CreateDefinitionAsync(entity, HttpContext.RequestAborted);

        _logger.LogInformation("Created tracker definition {Id}", created.Id);

        return CreatedAtAction(
            nameof(GetDefinition),
            new { id = created.Id },
            TrackerDefinitionDto.FromEntity(created)
        );
    }

    /// <summary>
    /// Update a tracker definition
    /// </summary>
    [HttpPut("definitions/{id:guid}")]
    [Authorize]
    [RemoteForm(Invalidates = ["GetDefinitions", "GetDefinition"])]
    [ProducesResponseType(typeof(TrackerDefinitionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackerDefinitionDto>> UpdateDefinition(
        Guid id,
        [FromBody] UpdateTrackerDefinitionRequest request
    )
    {
        var existing = await _repository.GetDefinitionByIdAsync(id, HttpContext.RequestAborted);
        if (existing == null)
            return NotFound();

        // Validate thresholds if being updated
        var mode = request.Mode ?? existing.Mode;
        var lifespan = request.LifespanHours ?? existing.LifespanHours;
        if (request.NotificationThresholds != null)
        {
            var validationError = ValidateThresholds(request.NotificationThresholds, lifespan, mode);
            if (validationError != null)
                return BadRequest(validationError);
        }

        // Validate mode-specific requirements
        if (mode == TrackerMode.Event && lifespan.HasValue)
            return Problem(detail: "Event mode trackers should not have a lifespan", statusCode: 400, title: "Bad Request");

        existing.Name = request.Name ?? existing.Name;
        existing.Description = request.Description ?? existing.Description;
        existing.Category = request.Category ?? existing.Category;
        existing.Icon = request.Icon ?? existing.Icon;
        existing.TriggerEventTypes =
            request.TriggerEventTypes != null
                ? JsonSerializer.Serialize(request.TriggerEventTypes)
                : existing.TriggerEventTypes;
        existing.TriggerNotesContains =
            request.TriggerNotesContains ?? existing.TriggerNotesContains;
        existing.LifespanHours = request.LifespanHours ?? existing.LifespanHours;
        existing.IsFavorite = request.IsFavorite ?? existing.IsFavorite;
        existing.DashboardVisibility = request.DashboardVisibility ?? existing.DashboardVisibility;
        existing.Visibility = request.Visibility ?? existing.Visibility;
        existing.StartEventType = request.StartEventType ?? existing.StartEventType;
        existing.CompletionEventType = request.CompletionEventType ?? existing.CompletionEventType;
        existing.Mode = request.Mode ?? existing.Mode;

        // Handle notification thresholds update (replaces all existing if provided)
        if (request.NotificationThresholds != null)
        {
            await _repository.UpdateNotificationThresholdsAsync(
                id,
                request
                    .NotificationThresholds.Select(t => new TrackerNotificationThresholdEntity
                    {
                        TrackerDefinitionId = id,
                        Urgency = t.Urgency,
                        Hours = t.Hours,
                        Description = t.Description,
                        DisplayOrder = t.DisplayOrder,
                    })
                    .ToList(),
                HttpContext.RequestAborted
            );
        }

        var updated = await _repository.UpdateDefinitionAsync(
            id,
            existing,
            HttpContext.RequestAborted
        );

        return Ok(TrackerDefinitionDto.FromEntity(updated!));
    }

    /// <summary>
    /// Delete a tracker definition. Returns 409 if alarm rules reference it.
    /// </summary>
    [HttpDelete("definitions/{id:guid}")]
    [Authorize]
    [RemoteCommand(Invalidates = ["GetDefinitions"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ReferencingRulesResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteDefinition(Guid id)
    {
        var existing = await _repository.GetDefinitionByIdAsync(id, HttpContext.RequestAborted);
        if (existing == null)
            return NotFound();

        // Refuse to delete a tracker definition that alarm rules reference via tracker
        // conditions. The caller must update or delete those rules first.
        var referencing = await _referenceService.FindRulesReferencingTrackerAsync(
            id, HttpContext.RequestAborted);
        if (referencing.Count > 0)
        {
            return Conflict(new ReferencingRulesResponse(
                referencing.Select(r => r.Id).ToList()));
        }

        await _repository.DeleteDefinitionAsync(id, HttpContext.RequestAborted);

        _logger.LogInformation("Deleted tracker definition {Id}", id);

        return NoContent();
    }

    #endregion

    #region Instances

    /// <summary>
    /// Get active tracker instances
    /// </summary>
    [HttpGet("instances")]
    [AllowAnonymous]
    [RemoteQuery]
    [ProducesResponseType(typeof(TrackerInstanceDto[]), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackerInstanceDto[]>> GetActiveInstances()
    {
        var instances = await _repository.GetActiveInstancesAsync(
            HttpContext.RequestAborted
        );

        return Ok(instances.Select(TrackerInstanceDto.FromEntity).ToArray());
    }

    /// <summary>
    /// Get completed tracker instances (history)
    /// </summary>
    [HttpGet("instances/history")]
    [Authorize]
    [RemoteQuery]
    [ProducesResponseType(typeof(TrackerInstanceDto[]), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackerInstanceDto[]>> GetInstanceHistory(
        [FromQuery] int limit = 100
    )
    {
        var instances = await _repository.GetCompletedInstancesAsync(
            limit,
            HttpContext.RequestAborted
        );

        return Ok(instances.Select(TrackerInstanceDto.FromEntity).ToArray());
    }

    /// <summary>
    /// Get upcoming tracker expirations for calendar
    /// </summary>
    [HttpGet("instances/upcoming")]
    [AllowAnonymous]
    [RemoteQuery]
    [ProducesResponseType(typeof(TrackerInstanceDto[]), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackerInstanceDto[]>> GetUpcomingInstances(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null
    )
    {
        var fromDate = from ?? DateTime.UtcNow;
        var toDate = to ?? DateTime.UtcNow.AddDays(30);

        var instances = await _repository.GetUpcomingInstancesAsync(
            fromDate,
            toDate,
            HttpContext.RequestAborted
        );

        return Ok(instances.Select(TrackerInstanceDto.FromEntity).ToArray());
    }

    /// <summary>
    /// Start a new tracker instance
    /// </summary>
    [HttpPost("instances")]
    [Authorize]
    [RemoteCommand(Invalidates = ["GetActiveInstances"])]
    [ProducesResponseType(typeof(TrackerInstanceDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TrackerInstanceDto>> StartInstance(
        [FromBody] StartTrackerInstanceRequest request
    )
    {
        // Verify definition exists
        var definition = await _repository.GetDefinitionByIdAsync(
            request.DefinitionId,
            HttpContext.RequestAborted
        );
        if (definition == null)
            return NotFound("Definition not found");

        // Validate mode-specific requirements
        if (definition.Mode == TrackerMode.Event && !request.ScheduledAt.HasValue)
            return Problem(detail: "Event mode trackers require a ScheduledAt datetime", statusCode: 400, title: "Bad Request");
        if (definition.Mode == TrackerMode.Duration && request.ScheduledAt.HasValue)
            return Problem(detail: "Duration mode trackers should not have a ScheduledAt datetime", statusCode: 400, title: "Bad Request");

        var instance = await _repository.StartInstanceAsync(
            request.DefinitionId,
            request.StartNotes,
            request.StartTreatmentId,
            request.StartedAt,
            request.ScheduledAt,
            HttpContext.RequestAborted
        );

        _logger.LogInformation(
            "Started tracker instance {Id} for definition {DefinitionId}",
            instance.Id,
            request.DefinitionId
        );

        // Broadcast via SignalR
        await _broadcast.BroadcastTrackerUpdateAsync(
            "create",
            TrackerInstanceDto.FromEntity(instance)
        );

        return CreatedAtAction(nameof(GetActiveInstances), TrackerInstanceDto.FromEntity(instance));
    }

    /// <summary>
    /// Complete a tracker instance
    /// </summary>
    [HttpPut("instances/{id:guid}/complete")]
    [Authorize]
    [RemoteCommand(Invalidates = ["GetActiveInstances", "GetInstanceHistory"])]
    [ProducesResponseType(typeof(TrackerInstanceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackerInstanceDto>> CompleteInstance(
        Guid id,
        [FromBody] CompleteTrackerInstanceRequest request
    )
    {
        var existing = await _repository.GetInstanceByIdAsync(id, HttpContext.RequestAborted);
        if (existing == null)
            return NotFound();

        if (existing.CompletedAt != null)
            return Problem(detail: "Instance already completed", statusCode: 400, title: "Bad Request");

        var completed = await _repository.CompleteInstanceAsync(
            id,
            request.Reason,
            request.CompletionNotes,
            request.CompleteTreatmentId,
            request.CompletedAt,
            HttpContext.RequestAborted
        );

        _logger.LogInformation(
            "Completed tracker instance {Id} with reason {Reason}",
            id,
            request.Reason
        );

        // Broadcast via SignalR
        await _broadcast.BroadcastTrackerUpdateAsync(
            "complete",
            TrackerInstanceDto.FromEntity(completed!)
        );

        return Ok(TrackerInstanceDto.FromEntity(completed!));
    }

    /// <summary>
    /// Delete a tracker instance
    /// </summary>
    [HttpDelete("instances/{id:guid}")]
    [Authorize]
    [RemoteCommand(Invalidates = ["GetActiveInstances"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteInstance(Guid id)
    {
        var existing = await _repository.GetInstanceByIdAsync(id, HttpContext.RequestAborted);
        if (existing == null)
            return NotFound();

        // Broadcast before deleting to get the definition name
        var dto = TrackerInstanceDto.FromEntity(existing);
        await _repository.DeleteInstanceAsync(id, HttpContext.RequestAborted);

        // Broadcast via SignalR
        await _broadcast.BroadcastTrackerUpdateAsync("delete", dto);

        return NoContent();
    }

    #endregion

    #region Presets

    /// <summary>
    /// Get all presets for the current tenant
    /// </summary>
    [HttpGet("presets")]
    [Authorize]
    [RemoteQuery]
    [ProducesResponseType(typeof(TrackerPresetDto[]), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackerPresetDto[]>> GetPresets()
    {
        var presets = await _repository.GetPresetsAsync(HttpContext.RequestAborted);

        return Ok(presets.Select(TrackerPresetDto.FromEntity).ToArray());
    }

    /// <summary>
    /// Create a new preset
    /// </summary>
    [HttpPost("presets")]
    [Authorize]
    [RemoteCommand(Invalidates = ["GetPresets"])]
    [ProducesResponseType(typeof(TrackerPresetDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TrackerPresetDto>> CreatePreset(
        [FromBody] CreateTrackerPresetRequest request
    )
    {
        var userId = HttpContext.GetSubjectIdString()!;

        // Verify definition exists
        var definition = await _repository.GetDefinitionByIdAsync(
            request.DefinitionId,
            HttpContext.RequestAborted
        );
        if (definition == null)
            return NotFound("Definition not found");

        var entity = new TrackerPresetEntity
        {
            UserId = userId,
            Name = request.Name,
            DefinitionId = request.DefinitionId,
            DefaultStartNotes = request.DefaultStartNotes,
        };

        var created = await _repository.CreatePresetAsync(entity, HttpContext.RequestAborted);

        _logger.LogInformation("Created tracker preset {Id}", created.Id);

        return Created(
            $"/api/v4/trackers/presets/{created.Id}",
            TrackerPresetDto.FromEntity(created)
        );
    }

    /// <summary>
    /// Apply a preset (starts a new instance)
    /// </summary>
    [HttpPost("presets/{id:guid}/apply")]
    [Authorize]
    [RemoteCommand(Invalidates = ["GetActiveInstances"])]
    [ProducesResponseType(typeof(TrackerInstanceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrackerInstanceDto>> ApplyPreset(
        Guid id,
        [FromBody] ApplyPresetRequest? request = null
    )
    {
        var instance = await _repository.ApplyPresetAsync(
            id,
            request?.OverrideNotes,
            HttpContext.RequestAborted
        );

        if (instance == null)
            return NotFound();

        _logger.LogInformation(
            "Applied preset {PresetId}, created instance {InstanceId}",
            id,
            instance.Id
        );

        return Ok(TrackerInstanceDto.FromEntity(instance));
    }

    /// <summary>
    /// Delete a preset
    /// </summary>
    [HttpDelete("presets/{id:guid}")]
    [Authorize]
    [RemoteCommand(Invalidates = ["GetPresets"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeletePreset(Guid id)
    {
        var existing = await _repository.GetPresetByIdAsync(id, HttpContext.RequestAborted);
        if (existing == null)
            return NotFound();

        await _repository.DeletePresetAsync(id, HttpContext.RequestAborted);

        return NoContent();
    }

    #endregion
}

#region DTOs

public class NotificationThresholdDto
{
    public Guid? Id { get; set; }
    public NotificationUrgency Urgency { get; set; }
    public int Hours { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }

    public static NotificationThresholdDto FromEntity(TrackerNotificationThresholdEntity entity) =>
        new()
        {
            Id = entity.Id,
            Urgency = entity.Urgency,
            Hours = entity.Hours,
            Description = entity.Description,
            DisplayOrder = entity.DisplayOrder,
        };
}

public class TrackerDefinitionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TrackerCategory Category { get; set; }
    public string Icon { get; set; } = "activity";
    public List<string> TriggerEventTypes { get; set; } = [];
    public string? TriggerNotesContains { get; set; }
    public int? LifespanHours { get; set; }

    // Notification thresholds (many-to-one relationship)
    public List<NotificationThresholdDto> NotificationThresholds { get; set; } = [];

    public bool IsFavorite { get; set; }

    /// <summary>
    /// Dashboard visibility: Off, Always, Info, Warn, Hazard, Urgent
    /// </summary>
    public DashboardVisibility DashboardVisibility { get; set; } = DashboardVisibility.Always;

    /// <summary>
    /// Visibility level for this tracker (Public, Private, RoleRestricted)
    /// </summary>
    public TrackerVisibility Visibility { get; set; } = TrackerVisibility.Public;

    /// <summary>
    /// Event type to create when tracker is started (for Nightscout compatibility)
    /// </summary>
    public string? StartEventType { get; set; }

    /// <summary>
    /// Event type to create when tracker is completed (for Nightscout compatibility)
    /// </summary>
    public string? CompletionEventType { get; set; }

    /// <summary>
    /// Tracker mode: Duration or Event
    /// </summary>
    public TrackerMode Mode { get; set; } = TrackerMode.Duration;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static TrackerDefinitionDto FromEntity(TrackerDefinitionEntity entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            Icon = entity.Icon,
            TriggerEventTypes =
                JsonSerializer.Deserialize<List<string>>(entity.TriggerEventTypes) ?? [],
            TriggerNotesContains = entity.TriggerNotesContains,
            LifespanHours = entity.LifespanHours,
            // Notification thresholds
            NotificationThresholds =
                entity
                    .NotificationThresholds?.OrderBy(t => t.DisplayOrder)
                    .Select(NotificationThresholdDto.FromEntity)
                    .ToList() ?? [],
            IsFavorite = entity.IsFavorite,
            DashboardVisibility = entity.DashboardVisibility,
            Visibility = entity.Visibility,
            StartEventType = entity.StartEventType,
            CompletionEventType = entity.CompletionEventType,
            Mode = entity.Mode,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
}

public class TrackerInstanceDto
{
    public Guid Id { get; set; }
    public Guid DefinitionId { get; set; }
    public string DefinitionName { get; set; } = string.Empty;
    public TrackerCategory Category { get; set; }
    public string Icon { get; set; } = "activity";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpectedEndAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? StartNotes { get; set; }
    public string? CompletionNotes { get; set; }
    public CompletionReason? CompletionReason { get; set; }
    public double AgeHours { get; set; }
    public bool IsActive { get; set; }

    public static TrackerInstanceDto FromEntity(TrackerInstanceEntity entity) =>
        new()
        {
            Id = entity.Id,
            DefinitionId = entity.DefinitionId,
            DefinitionName = entity.Definition?.Name ?? string.Empty,
            Category = entity.Definition?.Category ?? TrackerCategory.Custom,
            Icon = entity.Definition?.Icon ?? "activity",
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            ExpectedEndAt = entity.ExpectedEndAt,
            ScheduledAt = entity.ScheduledAt,
            StartNotes = entity.StartNotes,
            CompletionNotes = entity.CompletionNotes,
            CompletionReason = entity.CompletionReason,
            AgeHours = entity.AgeHours,
            IsActive = entity.IsActive,
        };
}

public class TrackerPresetDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid DefinitionId { get; set; }
    public string DefinitionName { get; set; } = string.Empty;
    public string? DefaultStartNotes { get; set; }
    public DateTime CreatedAt { get; set; }

    public static TrackerPresetDto FromEntity(TrackerPresetEntity entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            DefinitionId = entity.DefinitionId,
            DefinitionName = entity.Definition?.Name ?? string.Empty,
            DefaultStartNotes = entity.DefaultStartNotes,
            CreatedAt = entity.CreatedAt,
        };
}

#endregion

#region Request Models

public class CreateTrackerDefinitionRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TrackerCategory Category { get; set; } = TrackerCategory.Consumable;
    public string? Icon { get; set; }
    public List<string>? TriggerEventTypes { get; set; }
    public string? TriggerNotesContains { get; set; }
    public int? LifespanHours { get; set; }

    // Notification thresholds (many-to-one relationship)
    public List<CreateNotificationThresholdRequest>? NotificationThresholds { get; set; }
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Dashboard visibility: Off, Always, Info, Warn, Hazard, Urgent
    /// </summary>
    public DashboardVisibility DashboardVisibility { get; set; } = DashboardVisibility.Always;

    /// <summary>
    /// Visibility level for this tracker (Public, Private, RoleRestricted)
    /// </summary>
    public TrackerVisibility Visibility { get; set; } = TrackerVisibility.Public;

    /// <summary>
    /// Event type to create when tracker is started (for Nightscout compatibility)
    /// </summary>
    public string? StartEventType { get; set; }

    /// <summary>
    /// Event type to create when tracker is completed (for Nightscout compatibility)
    /// </summary>
    public string? CompletionEventType { get; set; }

    /// <summary>
    /// Tracker mode: Duration (time-based) or Event (scheduled datetime)
    /// </summary>
    public TrackerMode Mode { get; set; } = TrackerMode.Duration;
}

public class UpdateTrackerDefinitionRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public TrackerCategory? Category { get; set; }
    public string? Icon { get; set; }
    public List<string>? TriggerEventTypes { get; set; }
    public string? TriggerNotesContains { get; set; }
    public int? LifespanHours { get; set; }

    // Notification thresholds (if provided, replaces all existing thresholds)
    public List<CreateNotificationThresholdRequest>? NotificationThresholds { get; set; }
    public bool? IsFavorite { get; set; }

    /// <summary>
    /// Dashboard visibility: Off, Always, Info, Warn, Hazard, Urgent
    /// </summary>
    public DashboardVisibility? DashboardVisibility { get; set; }

    /// <summary>
    /// Visibility level for this tracker (Public, Private, RoleRestricted)
    /// </summary>
    public TrackerVisibility? Visibility { get; set; }

    /// <summary>
    /// Event type to create when tracker is started (for Nightscout compatibility)
    /// </summary>
    public string? StartEventType { get; set; }

    /// <summary>
    /// Event type to create when tracker is completed (for Nightscout compatibility)
    /// </summary>
    public string? CompletionEventType { get; set; }

    /// <summary>
    /// Tracker mode: Duration (time-based) or Event (scheduled datetime)
    /// </summary>
    public TrackerMode? Mode { get; set; }
}

public class CreateNotificationThresholdRequest
{
    public NotificationUrgency Urgency { get; set; }
    public int Hours { get; set; }
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}

public class StartTrackerInstanceRequest
{
    [Required]
    public Guid DefinitionId { get; set; }
    public string? StartNotes { get; set; }
    public string? StartTreatmentId { get; set; }

    /// <summary>
    /// Optional custom start time for backdating. Defaults to now if not provided.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// For Event mode: the scheduled datetime of the event
    /// </summary>
    public DateTime? ScheduledAt { get; set; }
}

public class CompleteTrackerInstanceRequest
{
    [Required]
    public CompletionReason Reason { get; set; }
    public string? CompletionNotes { get; set; }
    public string? CompleteTreatmentId { get; set; }

    /// <summary>
    /// Optional custom completion time for backdating. Defaults to now if not provided.
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}

public class CreateTrackerPresetRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid DefinitionId { get; set; }
    public string? DefaultStartNotes { get; set; }
}

public class ApplyPresetRequest
{
    public string? OverrideNotes { get; set; }
}

#endregion
