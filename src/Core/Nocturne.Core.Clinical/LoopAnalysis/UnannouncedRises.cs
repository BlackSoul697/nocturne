using Nocturne.Core.Clinical.Statistics;
using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.Core.Clinical.LoopAnalysis;

/// <summary>
/// Finds <see cref="EventAnchorKind.UnannouncedRise"/> anchors: the start of a meal-like glucose
/// rise that no carb entry announced, for a loop run fully closed.
/// </summary>
/// <remarks>
/// <para>
/// A rise starts from a floor: the <see cref="FloorQuantile"/> of a stable stretch of readings
/// (<see cref="StableMs"/> long, spanning at most <see cref="StableRangeMgdl"/>), at least
/// <see cref="MinimumFloorMgdl"/>. The floor, not any one reading, is what a rise is measured from,
/// so a single noisy reading neither sets the anchor nor hides the meal. The anchor is the last
/// reading within <see cref="FloorToleranceMgdl"/> of the floor before glucose climbs
/// <see cref="RiseMgdl"/> above it within <see cref="RiseWindowMs"/>, without falling back to the
/// floor on the way. No carb entry may fall within <see cref="EventAnchors.CarbFreeMs"/> of it.
/// </para>
/// <para>
/// Readings under a <c>DataExclusion</c> span are dropped before the search, a stretch or climb
/// is not read across a gap of more than <see cref="MaxGapMs"/>, and a climb faster than
/// <see cref="MaxRiseRateMgdlPerMinute"/> between two readings is not physiological, so a
/// compression low's recovery qualifies neither when it is excluded nor when it is not.
/// </para>
/// </remarks>
public static class UnannouncedRises
{
    /// <summary>
    /// How far above the floor glucose must climb. A meal without insulin raises glucose by well
    /// over 50 mg/dL; 40 keeps a meal the loop partly caught early while staying above the 20 to 30
    /// mg/dL a CGM's noise and an ordinary drift reach in the same time.
    /// </summary>
    public const double RiseMgdl = 40;

    /// <summary>
    /// How quickly, from the anchor: <see cref="RiseMgdl"/> in 45 minutes, about 0.9 mg/dL a minute,
    /// the slow end of what a meal's carbs drive (1 to 3). The dawn phenomenon usually climbs at 0.2
    /// to 0.5 and takes hours to rise as far; a steep one that passes this is what
    /// corroboration (<see cref="CorroborationBasalHours"/>) tells from a meal.
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
    /// The quantile of the stable stretch taken as its floor: low enough to sit at the level glucose
    /// rested at, high enough that one reading a few mg/dL low does not set it.
    /// </summary>
    public const double FloorQuantile = 25;

    /// <summary>
    /// How near the floor a reading is still at it: half the noise <see cref="StableRangeMgdl"/>
    /// allows between neighbouring readings.
    /// </summary>
    public const double FloorToleranceMgdl = 5;

    /// <summary>
    /// The lowest floor a rise is read from. Below 70 mg/dL a climb is far likelier the recovery from
    /// a hypo, treated or rebounding, than a meal, and a hypo treatment is often not logged as carbs.
    /// </summary>
    public const double MinimumFloorMgdl = 70;

    /// <summary>
    /// The longest gap between readings the search reads across: one missed five-minute reading.
    /// Longer, and what glucose did in between is unknown.
    /// </summary>
    public const long MaxGapMs = 10 * LocalSlotBinning.MinuteMs;

    /// <summary>
    /// The fastest climb between two readings read as glucose. Carbohydrate absorption peaks at
    /// about 3 to 4 mg/dL a minute; faster is a sensor artefact, typically a compression low ending.
    /// </summary>
    public const double MaxRiseRateMgdlPerMinute = 5;

    /// <summary>
    /// How long after one rise's anchor another is taken to be the same rise: a meal that stalls and
    /// climbs again within two hours is one meal, not two.
    /// </summary>
    public const long MergeMs = 2 * LocalSlotBinning.HourMs;

    /// <summary>
    /// How long after the anchor the loop's answer is read for corroboration: AID systems act on
    /// readings five minutes apart and their correction builds over half an hour or more, so 45
    /// minutes, the same as <see cref="RiseWindowMs"/>, covers their response latency.
    /// </summary>
    public const long CorroborationMs = RiseWindowMs;

    /// <summary>
    /// The net loop correction that corroborates a rise, in hours of the scheduled basal rate at the
    /// anchor: half an hour's schedule delivered above it within <see cref="CorroborationMs"/>. Scaled
    /// to the schedule so a low-dose user corroborates as easily as a high-dose one, and read off
    /// net loop correction (algorithm temp delta plus automatic boluses) so a loop that corrects with
    /// temp basals alone does as well as one that uses boluses. A slow dawn rise draws a trickle well
    /// short of this.
    /// </summary>
    public const double CorroborationBasalHours = 0.5;

    /// <param name="glucose">Readings in time order.</param>
    /// <param name="exclusions">Time whose readings never qualify.</param>
    /// <param name="insulin">Read only with <paramref name="requireCorroboration"/>.</param>
    public static List<EventAnchor> Find(
        IReadOnlyList<GlucoseSample> glucose,
        IntervalSet exclusions,
        IReadOnlyList<CarbEvent> carbs,
        NetInsulinAboveSchedule insulin,
        bool requireCorroboration)
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
            if (anchors.Count > 0 && readings[i].Mills < anchors[^1].Mills + MergeMs)
                continue;
            if (StableFloor(readings, stableStart, i) is not { } floor
                || Math.Abs(readings[i].Mgdl - floor) > FloorToleranceMgdl
                || ClimbFrom(readings, i, floor) is not { } anchor)
                continue;

            var at = readings[anchor].Mills;
            if (EventAnchors.AnyWithin(carbMills, at - EventAnchors.CarbFreeMs, at + EventAnchors.CarbFreeMs))
                continue;
            if (requireCorroboration && !Corroborated(insulin, at))
                continue;
            anchors.Add(new EventAnchor(at, EventAnchorKind.UnannouncedRise));
        }
        return anchors;
    }

    /// <summary>
    /// The floor of the stretch from <paramref name="first"/> to <paramref name="last"/>, when it
    /// reaches back to <see cref="StableMs"/> without a long gap, spans no more than
    /// <see cref="StableRangeMgdl"/> and sits at or above <see cref="MinimumFloorMgdl"/>.
    /// </summary>
    private static double? StableFloor(GlucoseSample[] readings, int first, int last)
    {
        if (readings[first].Mills > readings[last].Mills - StableMs + MaxGapMs)
            return null;
        var values = new List<double>(last - first + 1);
        for (var k = first; k <= last; k++)
        {
            if (k > first && readings[k].Mills - readings[k - 1].Mills > MaxGapMs)
                return null;
            values.Add(readings[k].Mgdl);
        }
        values.Sort();
        if (values[^1] - values[0] > StableRangeMgdl)
            return null;
        var floor = GlucoseStatistics.Percentile(values, FloorQuantile);
        return floor >= MinimumFloorMgdl ? floor : null;
    }

    /// <summary>
    /// Follows the readings after <paramref name="start"/>, which is at <paramref name="floor"/>:
    /// the anchor moves to each later reading still at the floor, and the climb qualifies once a
    /// reading is <see cref="RiseMgdl"/> above the floor within <see cref="RiseWindowMs"/> of it. A
    /// floor holds only for <see cref="StableMs"/> past the stretch it was read from; glucose resting
    /// longer is read against the floor of a later stretch, so a floor set as glucose came down from
    /// an earlier peak does not stand for hours of a lower one.
    /// </summary>
    /// <returns>The anchor's index, or null when no qualifying climb follows.</returns>
    private static int? ClimbFrom(GlucoseSample[] readings, int start, double floor)
    {
        var anchor = start;
        for (var j = start + 1; j < readings.Length; j++)
        {
            var minutes = (readings[j].Mills - readings[j - 1].Mills) / (double)LocalSlotBinning.MinuteMs;
            if (readings[j].Mills - readings[j - 1].Mills > MaxGapMs
                || (readings[j].Mgdl - readings[j - 1].Mgdl) / minutes > MaxRiseRateMgdlPerMinute
                || readings[j].Mgdl < floor - StableRangeMgdl)
                return null;
            if (readings[j].Mgdl <= floor + FloorToleranceMgdl)
            {
                if (readings[j].Mills - readings[start].Mills > StableMs)
                    return null;
                anchor = j;
                continue;
            }
            if (readings[j].Mills - readings[anchor].Mills > RiseWindowMs)
                return null;
            if (readings[j].Mgdl - floor >= RiseMgdl)
                return anchor;
        }
        return null;
    }

    /// <summary>
    /// Whether the loop answered the rise: net loop correction over <see cref="CorroborationMs"/> from
    /// the anchor of at least <see cref="CorroborationBasalHours"/> of the scheduled rate there. With
    /// no schedule known at the anchor, nothing corroborates it.
    /// </summary>
    public static bool Corroborated(NetInsulinAboveSchedule insulin, long anchor)
    {
        var pieces = insulin.Pieces;
        var i = SortedSearch.FirstEndingAfter(pieces, anchor, static p => p.End);
        if (i >= pieces.Count || pieces[i].Start > anchor || pieces[i].Scheduled is not { } scheduled || scheduled <= 0)
            return false;
        return insulin.Over(anchor, anchor + CorroborationMs).NetLoopCorrection >= CorroborationBasalHours * scheduled;
    }
}
