using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.Core.Contracts.Analytics;

/// <summary>
/// Loads what a Loop Analysis tab reads, once per request, and computes it for the primary range
/// and the comparison range.
/// </summary>
public interface ILoopAnalysisService
{
    /// <remarks>
    /// Unredacted, and without <see cref="DailyProfileResponse.HistoryClamped"/>: the caller narrows it
    /// to the reader's scopes and states the reader's history ceiling.
    /// </remarks>
    Task<DailyProfileResponse> GetDailyProfileAsync(LoopAnalysisFilter filter, CancellationToken ct = default);

    /// <remarks>Unredacted and without the history ceiling, as <see cref="GetDailyProfileAsync"/>.</remarks>
    Task<LoopHealthResponse> GetLoopHealthAsync(LoopAnalysisFilter filter, CancellationToken ct = default);
}
