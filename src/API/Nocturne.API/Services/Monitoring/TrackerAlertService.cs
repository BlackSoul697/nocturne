using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Monitoring;

/// <summary>
/// Represents an alert fired when a tracker crosses a configured threshold.
/// </summary>
public record TrackerAlert(
    Guid InstanceId,
    Guid DefinitionId,
    Guid ThresholdId,
    string TrackerName,
    NotificationUrgency Urgency,
    string Message
);

/// <summary>
/// Service to evaluate tracker instances against thresholds and generate alerts
/// </summary>
public interface ITrackerAlertService
{
    /// <summary>
    /// Evaluate all active tracker instances and generate pending alerts
    /// </summary>
    Task<List<TrackerAlert>> EvaluateActiveTrackersAsync(CancellationToken ct = default);

    /// <summary>
    /// Get pending (not yet displayed/sent) tracker alerts
    /// </summary>
    Task<List<TrackerAlert>> GetPendingAlertsAsync(CancellationToken ct = default);
}

public class TrackerAlertService : ITrackerAlertService
{
    private readonly ITrackerRepository _repository;
    private readonly ILogger<TrackerAlertService> _logger;

    public TrackerAlertService(
        ITrackerRepository repository,
        ILogger<TrackerAlertService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<TrackerAlert>> EvaluateActiveTrackersAsync(CancellationToken ct = default)
    {
        var alerts = new List<TrackerAlert>();

        // Get all active tracker instances for the tenant
        var instances = await _repository.GetActiveInstancesAsync(ct);

        foreach (var instance in instances)
        {
            var definition = instance.Definition;
            if (definition == null)
            {
                _logger.LogWarning("Tracker instance {InstanceId} has no definition", instance.Id);
                continue;
            }

            // Check each notification threshold
            foreach (var threshold in definition.NotificationThresholds.OrderBy(t => t.DisplayOrder))
            {
                var alert = EvaluateThreshold(instance, definition, threshold);
                if (alert != null)
                {
                    alerts.Add(alert);
                }
            }
        }

        return alerts;
    }

    /// <inheritdoc />
    public async Task<List<TrackerAlert>> GetPendingAlertsAsync(CancellationToken ct = default)
    {
        return await EvaluateActiveTrackersAsync(ct);
    }

    /// <summary>
    /// Evaluate a single threshold against an instance
    /// </summary>
    private TrackerAlert? EvaluateThreshold(
        TrackerInstanceEntity instance,
        TrackerDefinitionEntity definition,
        TrackerNotificationThresholdEntity threshold)
    {
        // Calculate effective hours based on mode
        double hoursFromReference;
        double effectiveThresholdHours;

        if (definition.Mode == TrackerMode.Event)
        {
            // Event mode: hours relative to ScheduledAt
            // Negative threshold = before event, Positive = after event
            if (!instance.ScheduledAt.HasValue)
            {
                _logger.LogWarning(
                    "Event mode tracker instance {InstanceId} has no ScheduledAt",
                    instance.Id);
                return null;
            }

            hoursFromReference = (DateTime.UtcNow - instance.ScheduledAt.Value).TotalHours;
            effectiveThresholdHours = threshold.Hours;
        }
        else
        {
            // Duration mode: hours relative to StartedAt
            // Negative thresholds are relative to lifespan end
            hoursFromReference = instance.AgeHours;

            if (threshold.Hours >= 0)
            {
                effectiveThresholdHours = threshold.Hours;
            }
            else
            {
                // Negative threshold: trigger X hours before lifespan ends
                if (!definition.LifespanHours.HasValue)
                {
                    _logger.LogWarning(
                        "Negative threshold on tracker {DefinitionId} without lifespan",
                        definition.Id);
                    return null;
                }
                effectiveThresholdHours = definition.LifespanHours.Value + threshold.Hours;
            }
        }

        // Check if threshold is crossed
        if (hoursFromReference < effectiveThresholdHours)
        {
            return null; // Not yet at threshold
        }

        // Generate the alert message
        var message = threshold.Description
            ?? GenerateDefaultMessage(definition, threshold, instance);

        return new TrackerAlert(
            InstanceId: instance.Id,
            DefinitionId: definition.Id,
            ThresholdId: threshold.Id,
            TrackerName: definition.Name,
            Urgency: threshold.Urgency,
            Message: message
        );
    }

    /// <summary>
    /// Generate default alert message based on mode and threshold
    /// </summary>
    private static string GenerateDefaultMessage(
        TrackerDefinitionEntity definition,
        TrackerNotificationThresholdEntity threshold,
        TrackerInstanceEntity instance)
    {
        if (definition.Mode == TrackerMode.Event)
        {
            if (threshold.Hours < 0)
            {
                return $"{definition.Name} in {Math.Abs(threshold.Hours)} hours";
            }
            return $"{definition.Name} was {threshold.Hours} hours ago";
        }

        return $"{definition.Name} has been active for {threshold.Hours} hours";
    }

}
