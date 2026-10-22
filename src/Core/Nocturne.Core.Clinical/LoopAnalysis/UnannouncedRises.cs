using Nocturne.Core.Clinical.Statistics;
using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.Core.Clinical.LoopAnalysis;

/// <summary>
/// Finds <see cref="EventAnchorKind.UnannouncedRise"/> anchors: the start of a meal-like glucose
/// rise that no carb entry announced, for a loop run fully closed.
/// </summary>
/// <remarks>
/// A rise qualifies when glucose climbs at least <see cref="RiseMgdl"/> within
/// <see cref="RiseWindowMs"/> of a reading that is the lowest of a stable stretch
/// (<see cref="StableMs"/>, <see cref="StableRangeMgdl"/>), never dipping below that reading on
/// the way up, and no carb entry falls within <see cref="EventAnchors.CarbFreeMs"/> of it. The
/// anchor is the last reading at that low before the climb. Readings under a <c>DataExclusion</c>
/// span are dropped before the search, and a stable stretch or rise with a gap of more than
/// <see cref="MaxGapMs"/> is not read across, so a compression low's recovery, whose low is
/// excluded, never qualifies.
/// </remarks>
public static class UnannouncedRises
{
    /// <summary>
    /// How far glucose must climb. A meal without insulin raises glucose by well over 50 mg/dL; 40
    /// keeps a meal the loop partly caught early while staying above the 20 to 30 mg/dL a CGM's
    /// noise and an ordinary drift reach in the same time.
    /// </summary>
    public const double RiseMgdl = 40;

    /// <summary>
    /// How quickly it must climb, at least <see cref="RiseMgdl"/> in 45 minutes: about 0.9 mg/dL a
    /// minute, inside the 1 to 3 a meal's carbs drive and above the 0.2 to 0.5 of the dawn
    /// phenomenon, which takes hours to rise as far.
    /// </summary>
    public const long RiseWindowMs = 45 * LocalSlotBinning.MinuteMs;

    /// <summary>
    /// How long glucose must have been stable before the rise: half an hour, so a rise is told from
    /// the continuation of one already under way, or the rebound from a fall.
    /// </summary>
    public const long StableMs = 30 * LocalSlotBinning.MinuteMs;

    /// <summary>
    /// The most the readings of the stable stretch may span. Two CGM readings five minutes apart
    /// differ by up to 10 mg/dL from noise alone, so 20 tolerates that while rejecting a trend.
    /// </summary>
    public const double StableRangeMgdl = 20;

    /// <summary>
    /// The longest gap between readings the search reads across: one missed five-minute reading.
    /// Longer, and what glucose did in between is unknown.
    /// </summary>
    public const long MaxGapMs = 10 * LocalSlotBinning.MinuteMs;

    /// <summary>
    /// Automatic bolus insulin within <see cref="ClusterMs"/> of the rise's start that corroborates
    /// it, when corroboration is asked for. An oref or Loop SMB answer to a meal is a run of boluses
    /// reaching this within half an hour, where its answer to a slow dawn rise is a trickle of tenths.
    /// </summary>
    public const double ClusterUnits = 0.5;

    public const long ClusterMs = 30 * LocalSlotBinning.MinuteMs;

    /// <summary>How far before an anchor the search reads.</summary>
    public const long LookbackMs = StableMs;

    /// <param name="glucose">Readings in time order.</param>
    /// <param name="exclusions">Time whose readings never qualify.</param>
    /// <param name="automaticDoses">Automatic boluses in time order; read only with <paramref name="requireCluster"/>.</param>
    public static List<EventAnchor> Find(
        IReadOnlyList<GlucoseSample> glucose,
        IntervalSet exclusions,
        IReadOnlyList<CarbEvent> carbs,
        IReadOnlyList<InsulinDose> automaticDoses,
        bool requireCluster)
    {
        var readings = glucose
            .Where(g => GlucoseStatistics.IsReading(g.Mgdl) && !exclusions.Contains(g.Mills))
            .ToArray();
        var carbMills = EventAnchors.CarbMills(carbs);
        var anchors = new List<EventAnchor>();

        var stableStart = 0;
        for (var i = 0; i < readings.Length; i++)
        {
            while (readings[stableStart].Mills < readings[i].Mills - StableMs)
                stableStart++;
            if (!IsStableLow(readings, stableStart, i))
                continue;
            if (RiseFrom(readings, i) is not { } top)
                continue;
            var low = LastAtLow(readings, i, top);
            if (EventAnchors.AnyWithin(carbMills, low.Mills - EventAnchors.CarbFreeMs, low.Mills + EventAnchors.CarbFreeMs))
                continue;
            if (requireCluster && AutomaticUnits(automaticDoses, low.Mills, low.Mills + ClusterMs) < ClusterUnits)
                continue;

            anchors.Add(new EventAnchor(low.Mills, EventAnchorKind.UnannouncedRise));
            i = top;
        }
        return anchors;
    }

    /// <summary>
    /// Whether reading <paramref name="i"/> ends a stable stretch as its lowest reading: the readings
    /// from <paramref name="first"/> reach back to its start without a long gap and span no more than
    /// <see cref="StableRangeMgdl"/>.
    /// </summary>
    private static bool IsStableLow(GlucoseSample[] readings, int first, int i)
    {
        var low = readings[i];
        if (readings[first].Mills > low.Mills - StableMs + MaxGapMs)
            return false;
        double min = low.Mgdl, max = low.Mgdl;
        for (var k = first; k < i; k++)
        {
            if (readings[k + 1].Mills - readings[k].Mills > MaxGapMs)
                return false;
            min = Math.Min(min, readings[k].Mgdl);
            max = Math.Max(max, readings[k].Mgdl);
        }
        return min >= low.Mgdl && max - min <= StableRangeMgdl;
    }

    /// <summary>
    /// The last reading before <paramref name="top"/> still at reading <paramref name="i"/>'s level:
    /// where the climb leaves the floor, rather than the first reading of a flat stretch.
    /// </summary>
    private static GlucoseSample LastAtLow(GlucoseSample[] readings, int i, int top)
    {
        var last = i;
        for (var k = i + 1; k < top; k++)
        {
            if (readings[k].Mgdl <= readings[i].Mgdl)
                last = k;
        }
        return readings[last];
    }

    /// <returns>The index of the first reading <see cref="RiseMgdl"/> above reading <paramref name="i"/>, or null.</returns>
    private static int? RiseFrom(GlucoseSample[] readings, int i)
    {
        var low = readings[i];
        for (var j = i + 1; j < readings.Length && readings[j].Mills <= low.Mills + RiseWindowMs; j++)
        {
            if (readings[j].Mills - readings[j - 1].Mills > MaxGapMs || readings[j].Mgdl < low.Mgdl)
                return null;
            if (readings[j].Mgdl - low.Mgdl >= RiseMgdl)
                return j;
        }
        return null;
    }

    private static double AutomaticUnits(IReadOnlyList<InsulinDose> doses, long from, long to)
    {
        double units = 0;
        for (var i = SortedSearch.FirstEndingAfter(doses, from - 1, static d => d.Mills); i < doses.Count && doses[i].Mills < to; i++)
            units += doses[i].Units;
        return units;
    }
}
