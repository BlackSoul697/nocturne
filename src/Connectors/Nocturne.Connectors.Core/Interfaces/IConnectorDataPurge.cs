using Nocturne.Core.Contracts.Audit;

namespace Nocturne.Connectors.Core.Interfaces;

/// <summary>
///     Removes the records a connector wrote that carry no data source id, which the source purge
///     cannot reach.
/// </summary>
public interface IConnectorDataPurge
{
    /// <summary>The connector name the purge covers, matched without regard to case.</summary>
    string ConnectorName { get; }

    /// <summary>
    ///     Deletes the current tenant's records as the source purge does: attributed to
    ///     <paramref name="auditContext"/>, so no sync re-imports them, unless
    ///     <paramref name="allowReimport"/> releases them and resets the connector's sync position.
    ///     Returns the deleted counts by data type.
    /// </summary>
    Task<IReadOnlyDictionary<string, long>> PurgeAsync(
        bool allowReimport, IAuditContext auditContext, CancellationToken ct);
}
