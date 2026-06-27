using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.Core.Interfaces;

public interface IMetadataPublisher
{
    Task<bool> PublishProfilesAsync(
        IEnumerable<Profile> profiles,
        string source,
        CancellationToken cancellationToken = default);

    Task<bool> PublishFoodAsync(
        IEnumerable<Food> foods,
        string source,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectorFoodEntry>?> PublishConnectorFoodEntriesAsync(
        IEnumerable<ConnectorFoodEntryImport> entries,
        string source,
        CancellationToken cancellationToken = default);

    Task<bool> PublishActivityAsync(
        IEnumerable<Activity> activities,
        string source,
        CancellationToken cancellationToken = default);

    Task<bool> PublishStateSpansAsync(
        IEnumerable<StateSpan> stateSpans,
        string source,
        CancellationToken cancellationToken = default);

    Task<bool> PublishSystemEventsAsync(
        IEnumerable<SystemEvent> systemEvents,
        string source,
        CancellationToken cancellationToken = default);

    Task<bool> PublishNotesAsync(
        IEnumerable<Note> records,
        string source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts body-weight measurements, keyed by the connector's deterministic <see cref="BodyWeight.Id"/>
    /// (a GUID) so re-syncs update in place rather than duplicating.
    /// </summary>
    Task<bool> PublishBodyWeightsAsync(
        IEnumerable<BodyWeight> records,
        string source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts step-count records, keyed by the connector's deterministic <see cref="StepCount.Id"/> (a GUID)
    /// so re-syncs update in place rather than duplicating.
    /// </summary>
    Task<bool> PublishStepCountsAsync(
        IEnumerable<StepCount> records,
        string source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts heart-rate records, keyed by the connector's deterministic <see cref="HeartRate.Id"/> (a GUID)
    /// so re-syncs update in place rather than duplicating.
    /// </summary>
    Task<bool> PublishHeartRatesAsync(
        IEnumerable<HeartRate> records,
        string source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the timestamp of the most recent activity record for the current tenant,
    /// used by connectors to resume catch-up from where they left off, or <c>null</c> if none exist.
    /// </summary>
    Task<DateTime?> GetLatestActivityTimestampAsync(
        string source,
        CancellationToken cancellationToken = default);
}
