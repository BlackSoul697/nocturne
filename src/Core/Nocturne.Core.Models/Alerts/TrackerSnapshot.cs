namespace Nocturne.Core.Models.Alerts;

/// <summary>
/// A point-in-time snapshot of a tracker definition's state for alarm evaluation.
/// Evaluators derive computed values (age, remaining, etc.) from these raw facts.
/// </summary>
public record TrackerSnapshot(
    Guid DefinitionId,
    Guid? InstanceId,
    TrackerCategory Category,
    TrackerMode Mode,
    DateTimeOffset? StartedAt,
    DateTimeOffset? ScheduledAt,
    decimal? LifespanHours,
    bool IsActive
);
