using Nocturne.Core.Clinical.Statistics;
using Nocturne.Core.Models;
using Nocturne.Core.Models.LoopAnalysis;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Clinical.LoopAnalysis;

/// <summary>Everything Loop health is computed from, fetched once for every range it covers.</summary>
public sealed record LoopHealthInputs
{
    public required TimeZoneInfo Zone { get; init; }

    /// <summary>
    /// Read past both ends of every range, so a gap or a temp that straddles an end is bounded by
    /// the cycles either side of it.
    /// </summary>
    public IReadOnlyList<ApsLoopCycle> Cycles { get; init; } = [];

    public required NetInsulinAboveSchedule Insulin { get; init; }

    public required BasalEvidence Evidence { get; init; }

    /// <summary>The scheduled basal the caps are multiples of; see <see cref="NetInsulinAboveSchedule.ScheduledBaseline"/>.</summary>
    public IReadOnlyList<RateSegment> Scheduled { get; init; } = [];

    /// <summary>Canonical readings.</summary>
    public IReadOnlyList<SensorGlucose> Glucose { get; init; } = [];

    /// <summary>Loop's maximum basal rate setting, U/h, where the profile in force records one.</summary>
    public StepTimeline<double>? LoopMaximumBasalRate { get; init; }
}

/// <summary>
/// Loop health: how the loop ran, figure by figure, for a range and for each local hour of the day.
/// </summary>
/// <remarks>
/// A share is time (or, for suggestions, events) measured over the analysed time. Each day and
/// each day's hour counts in the spread only where at least
/// <see cref="SlotSummary.MinimumSlotCoverage"/> of its wall-clock length was analysed, so a DST
/// day's long or short hour is weighed by its length. A count per day is normalised by the
/// analysed share of the day or hour it fell in.
/// </remarks>
public static class LoopHealthCalculator
{
    /// <summary>Two loop cycles further apart than this leave a gap: at least two cycles were missed.</summary>
    public const long LoopGapMs = 15 * LocalSlotBinning.MinuteMs;

    /// <summary>
    /// The time one cycle stands for in the activity shares, as <c>ApsSnapshotStrategy</c> counts
    /// cycles for <c>aid-system-metrics</c>.
    /// </summary>
    public const long CycleCreditMs = 5 * LocalSlotBinning.MinuteMs;

    /// <summary>A temp this close below the cap is at it: pumps set rates in 0.05 U/h steps.</summary>
    public const double MaxBasalToleranceUph = 0.05;

    /// <summary>IOB this close below a reported <c>max_iob</c> is at it.</summary>
    public const double MaxIobToleranceU = 0.1;

    private const int HoursPerDay = 24;
    private const long DefaultTempMs = 30 * LocalSlotBinning.MinuteMs;

    public static bool IsOref(AidAlgorithm algorithm) =>
        algorithm is AidAlgorithm.OpenAps or AidAlgorithm.AndroidAps or AidAlgorithm.Trio or AidAlgorithm.IAPS;

    public static LoopHealthRange Calculate(LoopHealthInputs inputs, LoopAnalysisMask mask, long from, long to)
    {
        var grid = new HourGrid(inputs.Zone, from, to, mask.Analysed);
        var cycles = Distinct(inputs.Cycles);

        return new LoopHealthRange
        {
            From = DateTimeOffset.FromUnixTimeMilliseconds(from).UtcDateTime,
            To = DateTimeOffset.FromUnixTimeMilliseconds(to).UtcDateTime,
            Days = grid.Days(),
            Activity = Activity(inputs, cycles, grid, from, to),
            Cadence = new LoopHealthCadence
            {
                LoopGaps = LoopGaps(cycles, grid, from, to),
                UnrecordedDelivery = UnrecordedDelivery(cycles, inputs.Evidence, grid, from, to),
            },
            ZeroDelivery = ZeroDelivery(inputs.Insulin, grid),
            MaxBasal = MaxBasal(inputs, cycles, grid),
            AtMaxIob = AtMaxIob(cycles, grid),
            AutomaticBoluses = AutomaticBoluses(inputs.Insulin, grid),
            SuggestedNotEnacted = SuggestedNotEnacted(cycles, grid),
            SensitivityRatio = grid.Pooled(cycles
                .Where(c => c.SensitivityRatio is not null && grid.IsAnalysed(c.Mills))
                .Select(c => (c.Mills, c.SensitivityRatio!.Value))),
            Limits = Limits(inputs, cycles, from, to),
        };
    }

    /// <summary>One cycle per instant, oldest first: two uploaders can post the same cycle.</summary>
    private static List<ApsLoopCycle> Distinct(IEnumerable<ApsLoopCycle> cycles) =>
        [.. cycles.GroupBy(c => c.Mills).OrderBy(g => g.Key).Select(g => g.First())];

    private static LoopHealthActivity Activity(
        LoopHealthInputs inputs, List<ApsLoopCycle> cycles, HourGrid grid, long from, long to)
    {
        var closed = grid.New();
        var pump = grid.New();
        var inRange = cycles.Where(c => c.Mills >= from && c.Mills < to).ToList();
        if (inRange.Count > 0)
        {
            foreach (var cycle in inRange)
            {
                grid.AddTime(pump, cycle.Mills, cycle.Mills + CycleCreditMs);
                if (cycle.Enacted)
                    grid.AddTime(closed, cycle.Mills, cycle.Mills + CycleCreditMs);
            }
        }
        else
        {
            foreach (var piece in inputs.Insulin.Pieces.Where(p => p.Delivered.HasValue))
            {
                if (piece.Origin is NetInsulinOrigin.AlgorithmTemp)
                    grid.AddTime(closed, piece.Start, piece.End);
                if (piece.Origin is NetInsulinOrigin.AlgorithmTemp or NetInsulinOrigin.ManualTemp or NetInsulinOrigin.Suspend)
                    grid.AddTime(pump, piece.Start, piece.End);
            }
        }

        var cgm = grid.New();
        foreach (var stream in inputs.Glucose.Where(g => GlucoseStatistics.IsReading(g.Mgdl))
                     .GroupBy(CanonicalGlucoseStream.StreamKey, StringComparer.Ordinal))
        {
            var ordered = stream.OrderBy(g => g.Mills).ToList();
            var creditMs = (long)(GlucoseCadence.SeriesCadenceMinutes(GlucoseCadence.ReadingIntervals(ordered))
                * LocalSlotBinning.MinuteMs);
            foreach (var reading in ordered)
                grid.AddTime(cgm, reading.Mills, reading.Mills + creditMs);
        }

        return new LoopHealthActivity
        {
            ClosedLoop = grid.Share(closed, grid.Analysed),
            Pump = grid.Share(pump, grid.Analysed),
            Cgm = grid.Share(cgm, grid.Analysed),
        };
    }

    private static LoopHealthGaps LoopGaps(List<ApsLoopCycle> cycles, HourGrid grid, long from, long to)
    {
        var gaps = new List<MillsInterval>();
        for (var i = 0; i + 1 < cycles.Count; i++)
        {
            if (cycles[i + 1].Mills - cycles[i].Mills > LoopGapMs)
                gaps.Add(new MillsInterval(cycles[i].Mills, cycles[i + 1].Mills));
        }
        return grid.Gaps(gaps, from, to);
    }

    /// <summary>
    /// Time the loop was cycling (no <see cref="LoopGapMs"/> gap) with no temp basal recorded, in
    /// stretches of at least <see cref="NetInsulinAboveSchedule.MaxInferredGapMs"/>.
    /// </summary>
    private static LoopHealthGaps UnrecordedDelivery(
        List<ApsLoopCycle> cycles, BasalEvidence evidence, HourGrid grid, long from, long to)
    {
        var looping = new List<MillsInterval>();
        for (var i = 0; i + 1 < cycles.Count; i++)
        {
            if (cycles[i + 1].Mills - cycles[i].Mills <= LoopGapMs)
                looping.Add(new MillsInterval(cycles[i].Mills, cycles[i + 1].Mills));
        }
        var unrecorded = IntervalSet.Of(looping).Subtract(evidence.Recorded).Intervals
            .Where(i => i.Length >= NetInsulinAboveSchedule.MaxInferredGapMs);
        return grid.Gaps(unrecorded, from, to);
    }

    private static LoopHealthZeroDelivery ZeroDelivery(NetInsulinAboveSchedule insulin, HourGrid grid)
    {
        var known = grid.New();
        var algorithm = grid.New();
        var manual = grid.New();
        var suspended = grid.New();
        foreach (var piece in insulin.Pieces)
        {
            if (piece.Delivered is not { } delivered)
                continue;
            grid.AddTime(known, piece.Start, piece.End);
            if (piece.Origin == NetInsulinOrigin.Suspend)
                grid.AddTime(suspended, piece.Start, piece.End);
            else if (delivered == 0 && piece.Origin == NetInsulinOrigin.AlgorithmTemp)
                grid.AddTime(algorithm, piece.Start, piece.End);
            else if (delivered == 0 && piece.Origin == NetInsulinOrigin.ManualTemp)
                grid.AddTime(manual, piece.Start, piece.End);
        }

        return new LoopHealthZeroDelivery
        {
            AlgorithmZeroTemp = grid.Share(algorithm, known),
            ManualZeroTemp = grid.Share(manual, known),
            Suspended = grid.Share(suspended, known),
        };
    }

    /// <summary>
    /// Each enacted temp runs from its cycle until the next cycle or its own duration, whichever
    /// comes first, and is measured against the cap in force when it was set.
    /// </summary>
    private static LoopHealthMaxBasal MaxBasal(LoopHealthInputs inputs, List<ApsLoopCycle> cycles, HourGrid grid)
    {
        var scheduled = StepTimeline<double>.LatestStartedWins(
            inputs.Scheduled.Select(s => new Step<double>(s.Start, s.End, s.Rate)));
        var dayMax = new Dictionary<DateOnly, double?>();
        double? HighestScheduledOn(long mills)
        {
            var day = LocalSlotBinning.LocalDay(mills, inputs.Zone);
            if (dayMax.TryGetValue(day, out var max))
                return max;
            var start = LocalSlotBinning.DayStartMills(day, inputs.Zone);
            var end = LocalSlotBinning.DayStartMills(day.AddDays(1), inputs.Zone);
            var rates = scheduled.Steps.Where(s => s.Start < end && s.End > start).Select(s => s.Value).ToList();
            return dayMax[day] = rates.Count > 0 ? rates.Max() : null;
        }

        var algorithmTime = grid.New();
        var known = grid.New();
        var atCap = grid.New();
        var bindingMs = new Dictionary<LoopHealthCap, long>();

        for (var i = 0; i < cycles.Count; i++)
        {
            var cycle = cycles[i];
            if (!cycle.Enacted || cycle.EnactedRate is not { } rate)
                continue;
            var end = Math.Min(
                i + 1 < cycles.Count ? cycles[i + 1].Mills : long.MaxValue,
                cycle.Mills + (cycle.EnactedDuration is { } minutes && minutes > 0 ? minutes * LocalSlotBinning.MinuteMs : DefaultTempMs));
            grid.AddTime(algorithmTime, cycle.Mills, end);

            if (!TryJudgeCap(cycle, rate, inputs.LoopMaximumBasalRate, scheduled, HighestScheduledOn, out var binding))
                continue;
            grid.AddTime(known, cycle.Mills, end);
            if (binding is { } binder)
            {
                var ms = grid.AddTime(atCap, cycle.Mills, end);
                bindingMs[binder] = bindingMs.GetValueOrDefault(binder) + ms;
            }
        }

        var atCapMs = bindingMs.Values.Sum();
        return new LoopHealthMaxBasal
        {
            AtCap = grid.Share(atCap, known),
            CapKnown = grid.Share(known, algorithmTime),
            Binding = [.. bindingMs
                .Where(b => b.Value > 0)
                .OrderByDescending(b => b.Value)
                .Select(b => new LoopHealthCapShare
                {
                    Cap = b.Key,
                    Minutes = SlotSummary.Round((double)b.Value / LocalSlotBinning.MinuteMs),
                    Percent = SlotSummary.Round(100.0 * b.Value / atCapMs),
                })],
        };
    }

    /// <summary>
    /// Whether a temp of <paramref name="rate"/> set by <paramref name="cycle"/> was at its cap.
    /// </summary>
    /// <param name="binding">The cap it was at, or null where it was below every cap.</param>
    /// <returns>False where the cap is unknown.</returns>
    private static bool TryJudgeCap(
        ApsLoopCycle cycle,
        double rate,
        StepTimeline<double>? loopMaximum,
        StepTimeline<double> scheduled,
        Func<long, double?> highestScheduledOn,
        out LoopHealthCap? binding)
    {
        binding = null;
        if (cycle.AidAlgorithm == AidAlgorithm.Loop)
        {
            if (loopMaximum is null || !loopMaximum.TryAt(cycle.Mills, out var maximum))
                return false;
            if (rate >= maximum - MaxBasalToleranceUph)
                binding = LoopHealthCap.LoopMaximumBasalRate;
            return true;
        }

        if (!IsOref(cycle.AidAlgorithm) || !cycle.HasReason)
            return false;

        var reason = OrefReason.Parse(cycle.LimitReason);
        if (cycle is { MaxBasal: { } maxBasal, MaxDailySafetyMultiplier: { } daily, CurrentBasalSafetyMultiplier: { } current }
            && scheduled.TryAt(cycle.Mills, out var scheduledRate)
            && highestScheduledOn(cycle.Mills) is { } highest)
        {
            (double Cap, LoopHealthCap Kind)[] caps =
            [
                (maxBasal, LoopHealthCap.OrefMaxBasal),
                (daily * highest, LoopHealthCap.OrefMaxDailySafetyMultiplier),
                (current * scheduledRate, LoopHealthCap.OrefCurrentBasalSafetyMultiplier),
            ];
            var lowest = caps.MinBy(c => c.Cap);
            if (reason.CappedAtMaxSafeBasal || rate >= lowest.Cap - MaxBasalToleranceUph)
                binding = lowest.Kind;
            return true;
        }

        if (reason.CappedAtMaxSafeBasal)
            binding = LoopHealthCap.OrefMaxSafeBasal;
        return true;
    }

    /// <summary>
    /// Each oref cycle with a reason stands until the next, or for <see cref="LoopGapMs"/>.
    /// It is at <c>max_iob</c> where its reason says so, or where IOB is within
    /// <see cref="MaxIobToleranceU"/> of the newest <c>max_iob</c> reported so far.
    /// </summary>
    private static LoopHealthShare? AtMaxIob(List<ApsLoopCycle> cycles, HourGrid grid)
    {
        var oref = grid.New();
        var atMax = grid.New();
        double? maxIob = null;
        var any = false;
        for (var i = 0; i < cycles.Count; i++)
        {
            var cycle = cycles[i];
            if (!IsOref(cycle.AidAlgorithm) || !cycle.HasReason)
                continue;
            var reason = OrefReason.Parse(cycle.LimitReason);
            maxIob = cycle.MaxIob ?? reason.MaxIob ?? maxIob;
            var end = Math.Min(i + 1 < cycles.Count ? cycles[i + 1].Mills : long.MaxValue, cycle.Mills + LoopGapMs);
            any |= grid.AddTime(oref, cycle.Mills, end) > 0;
            if (reason.AtMaxIob || cycle.Iob >= maxIob - MaxIobToleranceU)
                grid.AddTime(atMax, cycle.Mills, end);
        }
        return any ? grid.Share(atMax, oref) : null;
    }

    private static LoopHealthAutomaticBoluses AutomaticBoluses(NetInsulinAboveSchedule insulin, HourGrid grid)
    {
        var count = grid.New();
        var units = grid.New();
        var sizes = new List<(long, double)>();
        foreach (var dose in insulin.Doses)
        {
            if (!dose.Automatic || !grid.IsAnalysed(dose.Mills) || grid.IndexAt(dose.Mills) is not (var i and >= 0))
                continue;
            count[i]++;
            units[i] += dose.Units;
            sizes.Add((dose.Mills, dose.Units));
        }

        return new LoopHealthAutomaticBoluses
        {
            PerDay = grid.PerDay(count),
            UnitsPerDay = grid.PerDay(units),
            Size = grid.Pooled(sizes),
        };
    }

    private static LoopHealthShare? SuggestedNotEnacted(List<ApsLoopCycle> cycles, HourGrid grid)
    {
        var suggested = grid.New();
        var notEnacted = grid.New();
        var any = false;
        foreach (var cycle in cycles)
        {
            if (!IsOref(cycle.AidAlgorithm) || cycle.SuggestedChange != true || !grid.IsAnalysed(cycle.Mills)
                || grid.IndexAt(cycle.Mills) is not (var i and >= 0))
                continue;
            any = true;
            suggested[i]++;
            if (!cycle.Enacted)
                notEnacted[i]++;
        }
        return any ? grid.Share(notEnacted, suggested) : null;
    }

    private static LoopHealthLimits Limits(LoopHealthInputs inputs, List<ApsLoopCycle> cycles, long from, long to)
    {
        var oref = cycles.Where(c => c.Mills >= from && c.Mills < to && IsOref(c.AidAlgorithm)).ToList();
        double? Newest(Func<ApsLoopCycle, double?> value) => oref.Select(value).LastOrDefault(v => v is not null);
        var loop = inputs.LoopMaximumBasalRate?.Steps.LastOrDefault(s => s.Start < to && s.End > from);

        return new LoopHealthLimits
        {
            LoopMaximumBasalRate = loop is { } step ? step.Value : null,
            OrefMaxIob = Newest(c => c.MaxIob ?? OrefReason.Parse(c.LimitReason).MaxIob),
            OrefMaxBasal = Newest(c => c.MaxBasal),
            OrefMaxDailySafetyMultiplier = Newest(c => c.MaxDailySafetyMultiplier),
            OrefCurrentBasalSafetyMultiplier = Newest(c => c.CurrentBasalSafetyMultiplier),
        };
    }

    /// <summary>The range's local days by hour of the day, and its analysed time in each.</summary>
    private sealed class HourGrid
    {
        private readonly TimeZoneInfo _zone;
        private readonly IntervalSet _analysed;
        private readonly DateOnly _firstDay;
        private readonly int _dayCount;
        private readonly long[] _dayStarts;

        public HourGrid(TimeZoneInfo zone, long from, long to, IntervalSet analysed)
        {
            _zone = zone;
            _analysed = analysed;
            _firstDay = LocalSlotBinning.LocalDay(from, zone);
            _dayCount = to > from ? LocalSlotBinning.LocalDay(to - 1, zone).DayNumber - _firstDay.DayNumber + 1 : 0;
            _dayStarts = new long[_dayCount + 1];
            for (var d = 0; d <= _dayCount; d++)
                _dayStarts[d] = LocalSlotBinning.DayStartMills(_firstDay.AddDays(d), zone);

            Wall = New();
            Analysed = New();
            for (var d = 0; d < _dayCount; d++)
                Distribute(_dayStarts[d], _dayStarts[d + 1], (i, ms) => Wall[i] += ms);
            foreach (var interval in analysed.Intervals)
                Distribute(interval.Start, interval.End, (i, ms) => Analysed[i] += ms);
        }

        public double[] Wall { get; }

        public double[] Analysed { get; }

        public double[] New() => new double[_dayCount * HoursPerDay];

        public bool IsAnalysed(long mills) => _analysed.Contains(mills);

        public int IndexAt(long mills) => Index(LocalSlotBinning.LocalTime(mills, _zone));

        /// <summary>Adds the analysed part of <c>[start, end)</c> to <paramref name="target"/>.</summary>
        /// <returns>The milliseconds added.</returns>
        public long AddTime(double[] target, long start, long end)
        {
            long added = 0;
            _analysed.ForEachOverlap(start, end, (pieceStart, pieceEnd) =>
                Distribute(pieceStart, pieceEnd, (i, ms) =>
                {
                    target[i] += ms;
                    added += ms;
                }));
            return added;
        }

        public List<LoopHealthDay> Days()
        {
            var days = new List<LoopHealthDay>();
            for (var d = 0; d < _dayCount; d++)
            {
                if (Sum(Analysed, d) == 0)
                    continue;
                var date = _firstDay.AddDays(d);
                days.Add(new LoopHealthDay
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    DayOfWeek = date.DayOfWeek,
                    Start = DateTimeOffset.FromUnixTimeMilliseconds(_dayStarts[d]).UtcDateTime,
                });
            }
            return days;
        }

        public LoopHealthShare Share(double[] numerator, double[] denominator)
        {
            var hours = new List<LoopHealthShareHour>(HoursPerDay);
            for (var h = 0; h < HoursPerDay; h++)
            {
                double num = 0, den = 0;
                var daily = new List<double>();
                for (var d = 0; d < _dayCount; d++)
                {
                    var i = d * HoursPerDay + h;
                    num += numerator[i];
                    den += denominator[i];
                    if (Counts(Analysed[i], Wall[i]) && denominator[i] > 0)
                        daily.Add(Percent(numerator[i], denominator[i]));
                }
                hours.Add(new LoopHealthShareHour
                {
                    Hour = h,
                    Percent = den > 0 ? SlotSummary.Round(Percent(num, den)) : null,
                    Daily = SlotSummary.Of(daily),
                });
            }

            double totalNum = 0, totalDen = 0;
            var days = new List<double>();
            for (var d = 0; d < _dayCount; d++)
            {
                var num = Sum(numerator, d);
                var den = Sum(denominator, d);
                totalNum += num;
                totalDen += den;
                if (Counts(Sum(Analysed, d), Sum(Wall, d)) && den > 0)
                    days.Add(Percent(num, den));
            }

            return new LoopHealthShare
            {
                Percent = totalDen > 0 ? SlotSummary.Round(Percent(totalNum, totalDen)) : null,
                Daily = SlotSummary.Of(days),
                Hours = hours,
            };
        }

        /// <summary>
        /// <paramref name="amounts"/> per day: by hour, per standard hour of the hour's analysed
        /// time; for the range, per day scaled up to the day's length from its analysed share.
        /// </summary>
        public LoopHealthDistribution PerDay(double[] amounts)
        {
            var hours = new List<LoopHealthHourValue>(HoursPerDay);
            for (var h = 0; h < HoursPerDay; h++)
            {
                var values = new List<double>();
                for (var d = 0; d < _dayCount; d++)
                {
                    var i = d * HoursPerDay + h;
                    if (Counts(Analysed[i], Wall[i]))
                        values.Add(amounts[i] * LocalSlotBinning.HourMs / Analysed[i]);
                }
                hours.Add(new LoopHealthHourValue { Hour = h, Value = SlotSummary.Of(values) });
            }

            var days = new List<double>();
            for (var d = 0; d < _dayCount; d++)
            {
                var analysed = Sum(Analysed, d);
                var wall = Sum(Wall, d);
                if (Counts(analysed, wall))
                    days.Add(Sum(amounts, d) * wall / analysed);
            }
            return new LoopHealthDistribution { Overall = SlotSummary.Of(days), Hours = hours };
        }

        /// <summary>Every value pooled, by the hour it fell in and for the range; sparse by the days behind it.</summary>
        public LoopHealthDistribution Pooled(IEnumerable<(long Mills, double Value)> values)
        {
            var byHour = Enumerable.Range(0, HoursPerDay).Select(_ => new List<double>()).ToArray();
            var daysByHour = Enumerable.Range(0, HoursPerDay).Select(_ => new HashSet<int>()).ToArray();
            var all = new List<double>();
            var allDays = new HashSet<int>();
            foreach (var (mills, value) in values)
            {
                if (IndexAt(mills) is not (var i and >= 0))
                    continue;
                byHour[i % HoursPerDay].Add(value);
                daysByHour[i % HoursPerDay].Add(i / HoursPerDay);
                all.Add(value);
                allDays.Add(i / HoursPerDay);
            }

            return new LoopHealthDistribution
            {
                Overall = SlotSummary.Of(all, allDays.Count),
                Hours = [.. Enumerable.Range(0, HoursPerDay).Select(h => new LoopHealthHourValue
                {
                    Hour = h,
                    Value = SlotSummary.Of(byHour[h], daysByHour[h].Count),
                })],
            };
        }

        /// <summary>
        /// Gaps clipped to <c>[from, to)</c>, each counted once where any of it was analysed; the
        /// time share counts only the analysed part.
        /// </summary>
        public LoopHealthGaps Gaps(IEnumerable<MillsInterval> gaps, long from, long to)
        {
            var inGap = New();
            var byStartHour = new int[HoursPerDay];
            var kept = new List<LoopHealthGap>();
            foreach (var gap in gaps.OrderBy(g => g.Start))
            {
                var start = Math.Max(gap.Start, from);
                var end = Math.Min(gap.End, to);
                if (end <= start || AddTime(inGap, start, end) == 0)
                    continue;
                var local = LocalSlotBinning.LocalTime(start, _zone);
                byStartHour[local.Hour]++;
                kept.Add(new LoopHealthGap
                {
                    Start = DateTimeOffset.FromUnixTimeMilliseconds(start).UtcDateTime,
                    End = DateTimeOffset.FromUnixTimeMilliseconds(end).UtcDateTime,
                    Minutes = SlotSummary.Round((double)(end - start) / LocalSlotBinning.MinuteMs),
                    Date = DateOnly.FromDateTime(local).ToString("yyyy-MM-dd"),
                    StartMinuteOfDay = local.Hour * 60 + local.Minute,
                });
            }

            return new LoopHealthGaps
            {
                Count = kept.Count,
                TotalMinutes = SlotSummary.Round(kept.Sum(g => g.Minutes)),
                LongestMinutes = kept.Count > 0 ? kept.Max(g => g.Minutes) : null,
                CountByStartHour = [.. byStartHour],
                Time = Share(inGap, Analysed),
                Gaps = kept,
            };
        }

        private static bool Counts(double analysed, double wall) =>
            wall > 0 && analysed > 0 && analysed >= SlotSummary.MinimumSlotCoverage * wall;

        private static double Percent(double numerator, double denominator) =>
            Math.Min(100.0 * numerator / denominator, 100.0);

        private static double Sum(double[] values, int day)
        {
            double sum = 0;
            for (var h = 0; h < HoursPerDay; h++)
                sum += values[day * HoursPerDay + h];
            return sum;
        }

        private int Index(DateTime local)
        {
            var day = DateOnly.FromDateTime(local).DayNumber - _firstDay.DayNumber;
            return day < 0 || day >= _dayCount ? -1 : day * HoursPerDay + local.Hour;
        }

        private void Distribute(long start, long end, Action<int, long> add) =>
            LocalSlotBinning.Distribute(start, end, _zone, 60, (local, ms) =>
            {
                if (Index(local) is var i and >= 0)
                    add(i, ms);
            });
    }
}
