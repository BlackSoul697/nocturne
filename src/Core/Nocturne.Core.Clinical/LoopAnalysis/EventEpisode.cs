using Nocturne.Core.Clinical.Statistics;
using Nocturne.Core.Clinical.Treatments;
using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.Core.Clinical.LoopAnalysis;

/// <summary>
/// The window around an anchor, in slots of <see cref="EventResponseDefinition.SlotMinutes"/>
/// measured from the anchor's exact time rather than snapped to the wall clock.
/// </summary>
public readonly record struct EventWindow(long BeforeMs, long AfterMs)
{
    public const long SlotMs = EventResponseDefinition.SlotMinutes * LocalSlotBinning.MinuteMs;

    public static EventWindow Of(EventResponseDefinition definition) => new(
        definition.MinutesBefore * LocalSlotBinning.MinuteMs, definition.MinutesAfter * LocalSlotBinning.MinuteMs);

    public int SlotCount => (int)((BeforeMs + AfterMs) / SlotMs);

    /// <summary>Where slot <paramref name="slot"/> starts, relative to the anchor.</summary>
    public long SlotOffset(int slot) => slot * SlotMs - BeforeMs;
}

/// <summary>
/// The series an episode reads, indexed once for every anchor: readings outside the glucose
/// exclusions, insulin above schedule, and the IOB/COB ticks that stand as values.
/// </summary>
public sealed class EventSeries
{
    private readonly GlucoseSample[] _readings;
    private readonly DailyProfileInputs _inputs;

    public EventSeries(DailyProfileInputs inputs, IntervalSet glucoseExclusions)
    {
        _inputs = inputs;
        _readings = [.. inputs.Glucose
            .Where(g => GlucoseStatistics.IsReading(g.Mgdl) && !glucoseExclusions.Contains(g.Mills))
            .OrderBy(g => g.Mills)];
    }

    public IReadOnlyList<GlucoseSample> Readings => _readings;

    public NetInsulinAboveSchedule Insulin => _inputs.Insulin;

    /// <summary>The reading nearest <paramref name="mills"/>, no further than <paramref name="toleranceMs"/> from it.</summary>
    public double? GlucoseNear(long mills, long toleranceMs)
    {
        double? nearest = null;
        var best = long.MaxValue;
        for (var i = FirstReadingFrom(mills - toleranceMs); i < _readings.Length && _readings[i].Mills <= mills + toleranceMs; i++)
        {
            var distance = Math.Abs(_readings[i].Mills - mills);
            if (distance < best)
            {
                best = distance;
                nearest = _readings[i].Mgdl;
            }
        }
        return nearest;
    }

    /// <summary>The mean reading in <c>[start, end)</c>.</summary>
    public double? MeanGlucose(long start, long end)
    {
        double sum = 0;
        var count = 0;
        for (var i = FirstReadingFrom(start); i < _readings.Length && _readings[i].Mills < end; i++)
        {
            sum += _readings[i].Mgdl;
            count++;
        }
        return count > 0 ? sum / count : null;
    }

    /// <summary>Index of the first reading at or after <paramref name="mills"/>.</summary>
    public int FirstReadingFrom(long mills) =>
        SortedSearch.FirstEndingAfter(_readings, mills - 1, static r => r.Mills);

    /// <summary>
    /// IOB and COB over <c>[start, end)</c>, each tick weighted by the time it holds inside it; null
    /// where standing ticks cover less than <see cref="SlotSummary.MinimumSlotCoverage"/> of it.
    /// </summary>
    public (double Iob, double Cob)? IobCobOver(long start, long end)
    {
        var ticks = _inputs.IobCob;
        var interval = _inputs.IobCobIntervalMs;
        double iob = 0, cob = 0, covered = 0;
        for (var i = SortedSearch.FirstEndingAfter(ticks, start, t => t.Mills + interval); i < ticks.Count && ticks[i].Mills < end; i++)
        {
            if (!_inputs.Stands(ticks[i]))
                continue;
            var ms = Math.Min(end, ticks[i].Mills + interval) - Math.Max(start, ticks[i].Mills);
            if (ms <= 0)
                continue;
            iob += ticks[i].Iob * ms;
            cob += ticks[i].Cob * ms;
            covered += ms;
        }
        return covered > 0 && covered >= SlotSummary.MinimumSlotCoverage * (end - start)
            ? (iob / covered, cob / covered)
            : null;
    }

    public double? IobAt(long mills)
    {
        var ticks = _inputs.IobCob;
        var i = SortedSearch.FirstEndingAfter(ticks, mills, t => t.Mills + _inputs.IobCobIntervalMs);
        return i < ticks.Count && ticks[i].Mills <= mills && _inputs.Stands(ticks[i]) ? ticks[i].Iob : null;
    }

    /// <summary>
    /// Delivered basal and every bolus over <c>[start, end)</c>; null where the pump's records
    /// account for less than <paramref name="minimumCoverage"/> of its basal.
    /// </summary>
    public double? TotalInsulin(long start, long end, double minimumCoverage)
    {
        var pieces = Insulin.Pieces;
        double basal = 0;
        long covered = 0;
        for (var i = SortedSearch.FirstEndingAfter(pieces, start, static p => p.End); i < pieces.Count && pieces[i].Start < end; i++)
        {
            if (pieces[i].Delivered is not { } rate)
                continue;
            var ms = Math.Min(end, pieces[i].End) - Math.Max(start, pieces[i].Start);
            basal += rate * ms / LocalSlotBinning.HourMs;
            covered += ms;
        }
        if (covered < minimumCoverage * (end - start))
            return null;
        var boluses = Insulin.Over(start, end);
        return basal + boluses.AutomaticBolus + boluses.ManualBolus;
    }

    /// <summary>
    /// Net insulin above schedule over <c>[start, end)</c>; null where both delivery and the
    /// schedule are known for less than <paramref name="minimumCoverage"/> of it.
    /// </summary>
    public NetInsulin? NetInsulinOver(long start, long end, double minimumCoverage)
    {
        var net = Insulin.Over(start, end);
        return net.CoveredMs > 0 && net.CoveredMs >= minimumCoverage * (end - start) ? net : null;
    }
}

/// <summary>One relative slot of one episode; each value null where its data is missing.</summary>
public readonly record struct EpisodeSlot(
    double? GlucoseChange,
    double? Glucose,
    double? NetLoopCorrection,
    double? NetInsulinSinceAnchor,
    double? NetLoopCorrectionSinceAnchor,
    double? Iob,
    double? Cob);

/// <summary>
/// What glucose and insulin did around one anchor: every relative slot of its window, and the
/// figures read off it.
/// </summary>
/// <remarks>
/// A slot's rates follow the Daily profile's: net loop correction is the algorithm temp delta
/// over the time both delivery and the schedule are known plus automatic boluses over the slot, and
/// counts only where that time is at least <see cref="SlotSummary.MinimumSlotCoverage"/> of the
/// slot. Totals over a longer stretch (the window, the first 30 to 120 minutes) need
/// <see cref="DailyProfileCalculator.InsulinSummaryMinimumCoverage"/> of it, since a gap there
/// would be read as insulin not given.
/// </remarks>
public sealed class EventEpisode
{
    /// <summary>
    /// How far from the anchor, or from two and four hours after it, a reading may be and still
    /// stand for glucose then: one five-minute CGM interval.
    /// </summary>
    public const long ReadingToleranceMs = 5 * LocalSlotBinning.MinuteMs;

    /// <summary>The band around the start glucose that counts as back to start.</summary>
    public const double BackToStartMgdl = 20;

    private EventEpisode(EventAnchor anchor, EpisodeSlot[] slots, EventMetrics metrics)
    {
        Anchor = anchor;
        Slots = slots;
        Metrics = metrics;
    }

    public EventAnchor Anchor { get; }

    public IReadOnlyList<EpisodeSlot> Slots { get; }

    public EventMetrics Metrics { get; }

    /// <returns>Null when no reading stands for the anchor glucose.</returns>
    public static EventEpisode? Build(EventAnchor anchor, EventSeries series, EventWindow window)
    {
        if (series.GlucoseNear(anchor.Mills, ReadingToleranceMs) is not { } start)
            return null;

        var slots = new EpisodeSlot[window.SlotCount];
        for (var k = 0; k < slots.Length; k++)
            slots[k] = Slot(anchor.Mills, anchor.Mills + window.SlotOffset(k), start, series);

        return new EventEpisode(anchor, slots, Measure(anchor, start, series, window));
    }

    private static EpisodeSlot Slot(long anchor, long slotStart, double start, EventSeries series)
    {
        var slotEnd = slotStart + EventWindow.SlotMs;
        var glucose = series.MeanGlucose(slotStart, slotEnd);

        double? correction = null;
        var net = series.Insulin.Over(slotStart, slotEnd);
        if (net.CoveredMs > 0 && net.CoveredMs >= SlotSummary.MinimumSlotCoverage * EventWindow.SlotMs)
            correction = net.AlgorithmTemp * LocalSlotBinning.HourMs / net.CoveredMs
                + net.AutomaticBolus * LocalSlotBinning.HourMs / EventWindow.SlotMs;

        var sinceAnchor = slotStart >= anchor
            ? series.NetInsulinOver(anchor, slotEnd, SlotSummary.MinimumSlotCoverage)
            : null;
        var iobCob = series.IobCobOver(slotStart, slotEnd);

        return new EpisodeSlot(
            glucose - start,
            glucose,
            correction,
            sinceAnchor?.Total,
            sinceAnchor?.NetLoopCorrection,
            iobCob?.Iob,
            iobCob?.Cob);
    }

    private static EventMetrics Measure(EventAnchor anchor, double start, EventSeries series, EventWindow window)
    {
        var a = anchor.Mills;
        var end = a + window.AfterMs;
        var coverage = DailyProfileCalculator.InsulinSummaryMinimumCoverage;
        var (peak, peakAt, back) = Excursion(a, end, start, series);
        var net = series.NetInsulinOver(a - window.BeforeMs, end, coverage)?.Total;
        double? First(long minutes) => minutes * LocalSlotBinning.MinuteMs <= window.AfterMs
            ? series.NetInsulinOver(a, a + minutes * LocalSlotBinning.MinuteMs, coverage)?.Total
            : null;
        double? GlucoseAfter(long hours) => hours * LocalSlotBinning.HourMs <= window.AfterMs
            ? series.GlucoseNear(a + hours * LocalSlotBinning.HourMs, ReadingToleranceMs)
            : null;

        return new EventMetrics
        {
            StartGlucose = start,
            PeakRise = peak - start,
            MinutesToPeak = Minutes(a, peakAt),
            GlucoseAt2Hours = GlucoseAfter(2),
            GlucoseAt4Hours = GlucoseAfter(4),
            MinutesBackToStart = Minutes(a, back),
            TotalInsulin = series.TotalInsulin(a - window.BeforeMs, end, coverage),
            NetInsulin = net,
            IobAtAnchor = series.IobAt(a),
            IobAtEnd = series.IobAt(end),
            GramsPerUnit = anchor.Grams is { } grams && net is > 0 ? grams / net : null,
            NetInsulinFirst30 = First(30),
            NetInsulinFirst60 = First(60),
            NetInsulinFirst120 = First(120),
        };
    }

    /// <summary>
    /// The highest reading after the anchor and when it came, and when glucose was back within
    /// <see cref="BackToStartMgdl"/> of the start after its furthest excursion either way: the
    /// anchor itself if it never left, null if it had not come back by <paramref name="end"/>.
    /// </summary>
    private static (double? Peak, long? PeakAt, long? Back) Excursion(long anchor, long end, double start, EventSeries series)
    {
        var readings = series.Readings;
        double? peak = null;
        long? peakAt = null;
        var furthest = -1;
        double furthestDistance = 0;
        var first = series.FirstReadingFrom(anchor + 1);
        for (var i = first; i < readings.Count && readings[i].Mills < end; i++)
        {
            if (peak is null || readings[i].Mgdl > peak)
            {
                peak = readings[i].Mgdl;
                peakAt = readings[i].Mills;
            }
            var distance = Math.Abs(readings[i].Mgdl - start);
            if (distance > furthestDistance)
            {
                furthestDistance = distance;
                furthest = i;
            }
        }

        if (peak is null)
            return (null, null, null);
        if (furthestDistance <= BackToStartMgdl)
            return (peak, peakAt, anchor);
        for (var i = furthest + 1; i < readings.Count && readings[i].Mills < end; i++)
        {
            if (Math.Abs(readings[i].Mgdl - start) <= BackToStartMgdl)
                return (peak, peakAt, readings[i].Mills);
        }
        return (peak, peakAt, null);
    }

    private static double? Minutes(long from, long? to) =>
        to is { } at ? SlotSummary.Round((at - from) / (double)LocalSlotBinning.MinuteMs) : null;
}
