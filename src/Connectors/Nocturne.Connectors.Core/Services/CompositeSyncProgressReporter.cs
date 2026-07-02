using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;

namespace Nocturne.Connectors.Core.Services;

/// <summary>
/// Fans a sync progress event out to multiple reporters, so a sync can stream to SignalR while a
/// background job simultaneously records the latest event into its pollable status. A failing
/// reporter is logged and skipped — progress reporting must never fail the sync itself.
/// </summary>
/// <seealso cref="ISyncProgressReporter"/>
public class CompositeSyncProgressReporter : ISyncProgressReporter
{
    private readonly IReadOnlyList<ISyncProgressReporter> _reporters;
    private readonly ILogger? _logger;

    /// <summary>Initializes a new instance of <see cref="CompositeSyncProgressReporter"/>.</summary>
    /// <param name="reporters">The reporters to fan out to, invoked in order.</param>
    /// <param name="logger">Optional logger for reporter failures.</param>
    public CompositeSyncProgressReporter(
        IReadOnlyList<ISyncProgressReporter> reporters,
        ILogger? logger = null)
    {
        _reporters = reporters;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ReportProgressAsync(SyncProgressEvent progress, CancellationToken ct = default)
    {
        foreach (var reporter in _reporters)
        {
            try
            {
                await reporter.ReportProgressAsync(progress, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Sync progress reporter {Reporter} failed; continuing",
                    reporter.GetType().Name);
            }
        }
    }
}
