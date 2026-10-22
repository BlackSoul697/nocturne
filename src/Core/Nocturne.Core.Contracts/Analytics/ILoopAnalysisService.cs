using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.Core.Contracts.Analytics;

/// <summary>
/// Loads what a Loop Analysis tab reads, once per request, and computes it for the primary range
/// and the comparison range.
/// </summary>
public interface ILoopAnalysisService
{
    /// <remarks>
    /// <paramref name="filter"/> is resolved: its ranges are instants in <paramref name="zone"/>
    /// that end after they start. The result is unredacted and carries none of the
    /// <see cref="LoopAnalysisResponse"/> envelope, which the caller fills.
    /// </remarks>
    Task<DailyProfileResponse> GetDailyProfileAsync(
        LoopAnalysisFilter filter, TimeZoneInfo zone, CancellationToken ct = default);

    /// <inheritdoc cref="GetDailyProfileAsync"/>
    /// <param name="definition">The cohorts and window, already narrowed to the caller.</param>
    Task<EventResponseReport> GetEventResponseAsync(
        LoopAnalysisFilter filter, EventResponseDefinition definition, TimeZoneInfo zone, CancellationToken ct = default);
}
