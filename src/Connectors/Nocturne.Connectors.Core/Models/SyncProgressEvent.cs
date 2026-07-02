namespace Nocturne.Connectors.Core.Models;

public class SyncProgressEvent
{
    public required string ConnectorId { get; set; }
    public required string ConnectorName { get; set; }
    public SyncPhase Phase { get; set; }
    public SyncDataType? CurrentDataType { get; set; }
    public List<SyncDataType> CompletedDataTypes { get; set; } = [];
    public int TotalDataTypes { get; set; }
    public Dictionary<SyncDataType, int> ItemsSyncedSoFar { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public SyncMessageType? MessageType { get; set; }
    public Dictionary<string, string>? MessageParams { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Lower bound of the date window being fetched for the current data type. Null when the
    /// fetch has no lower bound (full-history import), in which case no fraction can be computed.
    /// </summary>
    public DateTime? WindowStart { get; set; }

    /// <summary>Upper bound of the date window being fetched for the current data type.</summary>
    public DateTime? WindowEnd { get; set; }

    /// <summary>
    /// The pagination cursor within the window. Connectors page backwards from
    /// <see cref="WindowEnd"/> towards <see cref="WindowStart"/>, so the fraction of the window
    /// already covered is (WindowEnd - CurrentPosition) / (WindowEnd - WindowStart).
    /// </summary>
    public DateTime? CurrentPosition { get; set; }
}
