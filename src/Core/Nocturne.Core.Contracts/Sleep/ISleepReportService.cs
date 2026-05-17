using Nocturne.Core.Models;
using Nocturne.Core.Models.Sleep.Report;

namespace Nocturne.Core.Contracts.Sleep;

public interface ISleepReportService
{
    /// <summary>
    /// Returns the full single-night report for the given session, or null if the session
    /// does not exist or belongs to a different tenant.
    /// </summary>
    Task<SleepSingleNightReport?> GetSingleNightReportAsync(
        Guid sessionId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a trends report covering all sessions in the date range (max 90 days).
    /// When <paramref name="source"/> is null, deduplicates to one session per calendar
    /// night (longest TotalSleepMs wins; tie-break by source priority).
    /// </summary>
    Task<SleepTrendsReport> GetTrendsReportAsync(
        DateTime from,
        DateTime to,
        SleepSource? source = null,
        CancellationToken ct = default);
}
