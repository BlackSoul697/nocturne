using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Abstractions;

/// <summary>
/// Repository port for Tracker operations (definitions, instances, presets)
/// </summary>
public interface ITrackerRepository
{
    // Definitions

    /// <summary>
    /// Gets all tracker definitions for the current tenant
    /// </summary>
    Task<List<TrackerDefinitionEntity>> GetDefinitionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tracker definitions filtered by category
    /// </summary>
    Task<List<TrackerDefinitionEntity>> GetDefinitionsByCategoryAsync(
        TrackerCategory category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tracker definitions marked as favorites
    /// </summary>
    Task<TrackerDefinitionEntity[]> GetFavoriteDefinitionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific tracker definition by its identifier
    /// </summary>
    Task<TrackerDefinitionEntity?> GetDefinitionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new tracker definition
    /// </summary>
    Task<TrackerDefinitionEntity> CreateDefinitionAsync(
        TrackerDefinitionEntity definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing tracker definition
    /// </summary>
    Task<TrackerDefinitionEntity?> UpdateDefinitionAsync(
        Guid id,
        TrackerDefinitionEntity updated,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tracker definition
    /// </summary>
    Task<bool> DeleteDefinitionAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the notification thresholds for a tracker definition
    /// </summary>
    Task UpdateNotificationThresholdsAsync(
        Guid definitionId,
        List<TrackerNotificationThresholdEntity> thresholds,
        CancellationToken cancellationToken = default);

    // Instances

    /// <summary>
    /// Gets all active tracker instances for the current tenant
    /// </summary>
    Task<TrackerInstanceEntity[]> GetActiveInstancesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active instances for a specific tracker definition
    /// </summary>
    Task<List<TrackerInstanceEntity>> GetActiveInstancesForDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets completed tracker instances, with an optional limit
    /// </summary>
    Task<TrackerInstanceEntity[]> GetCompletedInstancesAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets upcoming scheduled tracker instances within a date range
    /// </summary>
    Task<TrackerInstanceEntity[]> GetUpcomingInstancesAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific tracker instance by its identifier
    /// </summary>
    Task<TrackerInstanceEntity?> GetInstanceByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a new instance of a tracker definition
    /// </summary>
    Task<TrackerInstanceEntity> StartInstanceAsync(
        Guid definitionId,
        string? startNotes = null,
        string? startTreatmentId = null,
        DateTime? startedAt = null,
        DateTime? scheduledAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes an active tracker instance with a specified reason
    /// </summary>
    Task<TrackerInstanceEntity?> CompleteInstanceAsync(
        Guid instanceId,
        CompletionReason reason,
        string? completionNotes = null,
        string? completeTreatmentId = null,
        DateTime? completedAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tracker instance
    /// </summary>
    Task<bool> DeleteInstanceAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // Presets

    /// <summary>
    /// Gets all tracker presets for the current tenant
    /// </summary>
    Task<TrackerPresetEntity[]> GetPresetsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific tracker preset by its identifier
    /// </summary>
    Task<TrackerPresetEntity?> GetPresetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new tracker preset
    /// </summary>
    Task<TrackerPresetEntity> CreatePresetAsync(
        TrackerPresetEntity preset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a tracker preset, creating a new instance
    /// </summary>
    Task<TrackerInstanceEntity?> ApplyPresetAsync(
        Guid presetId,
        string? overrideNotes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tracker preset
    /// </summary>
    Task<bool> DeletePresetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // Snapshots (alarm engine)

    /// <summary>
    /// Returns tracker snapshots for all definitions in the tenant (active and inactive instances).
    /// Used by the alarm engine to evaluate tracker conditions.
    /// </summary>
    Task<IReadOnlyList<TrackerSnapshot>> GetTrackerSnapshotsAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns tracker snapshots as of a specific point in time (for replay/simulator).
    /// </summary>
    Task<IReadOnlyList<TrackerSnapshot>> GetTrackerSnapshotsAsOfAsync(DateTimeOffset asOf, CancellationToken ct = default);
}
