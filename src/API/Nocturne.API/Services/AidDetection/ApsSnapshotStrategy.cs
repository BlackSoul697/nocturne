using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.AidDetection;

/// <summary>
/// Calculates <see cref="AidSegmentMetrics"/> for APS-based closed-loop algorithms
/// by counting <see cref="AidDetectionContext.LoopCycles"/> within the evaluation window.
/// </summary>
/// <remarks>
/// Assumes a fixed 5-minute loop cycle interval. AID-active percentage is derived from
/// enacted snapshots; pump-use percentage is derived from the total snapshot count.
/// </remarks>
/// <seealso cref="IAidDetectionStrategy"/>
public class ApsSnapshotStrategy : IAidDetectionStrategy
{
    private const double LoopCycleIntervalMinutes = 5.0;

    /// <inheritdoc/>
    public IReadOnlySet<AidAlgorithm> SupportedAlgorithms { get; } = new HashSet<AidAlgorithm>
    {
        AidAlgorithm.OpenAps,
        AidAlgorithm.AndroidAps,
        AidAlgorithm.Loop,
        AidAlgorithm.Trio,
        AidAlgorithm.IAPS
    };

    /// <inheritdoc/>
    /// <remarks>
    /// Returns an empty <see cref="AidSegmentMetrics"/> when no APS snapshots are present
    /// in the supplied <paramref name="context"/>.
    /// </remarks>
    public AidSegmentMetrics CalculateMetrics(AidDetectionContext context)
    {
        var snapshots = context.LoopCycles;

        if (snapshots.Count == 0)
        {
            return new AidSegmentMetrics();
        }

        var totalMinutes = (context.EndDate - context.StartDate).TotalMinutes;
        var totalCount = snapshots.Count;
        var enactedCount = snapshots.Count(s => s.Enacted);

        return new AidSegmentMetrics
        {
            LoopCycleCount = totalCount,
            EnactedCount = enactedCount,
            AidActivePercent = Math.Min(enactedCount * LoopCycleIntervalMinutes / totalMinutes * 100.0, 100.0),
            PumpUsePercent = Math.Min(totalCount * LoopCycleIntervalMinutes / totalMinutes * 100.0, 100.0)
        };
    }
}
