using Nocturne.Core.Clinical.Statistics;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Basal;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Clinical.LoopAnalysis;

/// <summary>A constant rate, U/h, over <c>[Start, End)</c>.</summary>
public readonly record struct RateSegment(long Start, long End, double Rate);

public readonly record struct DeliveredBasalSegment(long Start, long End, double Rate, BasalDeliveryOrigin Origin);

public readonly record struct InsulinDose(long Mills, double Units, bool Automatic);

/// <summary>An override's insulin-needs scale factor over <c>[Start, End)</c>.</summary>
public readonly record struct ScaleFactorInterval(long Start, long End, double Factor);

/// <summary>
/// Where insulin above schedule came from. Every AID system's correction is
/// <see cref="AlgorithmTemp"/> plus <see cref="AutomaticBolus"/>, whichever of the two it uses.
/// </summary>
public enum NetInsulinOrigin
{
    /// <summary>The algorithm's temp basals, zero-temps included.</summary>
    AlgorithmTemp,
    AutomaticBolus,
    ManualTemp,
    Suspend,

    /// <summary>
    /// No temp running, whether the pump said so or a gap was filled from the schedule. Zero
    /// except where the baseline is scaled by an override the pump schedule does not carry.
    /// </summary>
    Inferred,
    ManualBolus,
}

/// <summary>Insulin above schedule over one interval, U, by origin.</summary>
public sealed record NetInsulin(
    double AlgorithmTemp,
    double AutomaticBolus,
    double ManualTemp,
    double Suspend,
    double Inferred,
    double ManualBolus,
    long CoveredMs)
{
    /// <summary>The algorithm's correction alone: the figure that means the same for every AID system.</summary>
    public double NetLoopCorrection => AlgorithmTemp + AutomaticBolus;

    public double Total => AlgorithmTemp + AutomaticBolus + ManualTemp + Suspend + Inferred + ManualBolus;
}

/// <summary>
/// One stretch of the basal timeline with a constant delivered and scheduled rate. Either is null
/// where it is unknown; only a piece with both has a delta.
/// </summary>
public readonly record struct BasalPiece(
    long Start, long End, double? Delivered, double? Scheduled, NetInsulinOrigin? Origin)
{
    public bool HasDelta => Delivered.HasValue && Scheduled.HasValue;
}

/// <summary>
/// Delivered basal minus scheduled basal, plus every bolus, over any interval, split by
/// <see cref="NetInsulinOrigin"/>. The definition every Loop Analysis tab reads its insulin from.
/// </summary>
/// <remarks>
/// Time where either delivery or the schedule is unknown contributes no basal delta, rather than
/// a delta against zero; <see cref="NetInsulin.CoveredMs"/> says how much of an interval had both.
/// Boluses count wherever they fall.
/// </remarks>
public sealed class NetInsulinAboveSchedule
{
    private const double HourMs = 3_600_000d;

    private readonly BasalPiece[] _pieces;
    private readonly InsulinDose[] _doses;

    /// <param name="delivered">Non-overlapping; see <see cref="DeliveredFromSeries"/>.</param>
    /// <param name="scheduled">Non-overlapping; see <see cref="ScheduledBaseline"/>.</param>
    public NetInsulinAboveSchedule(
        IEnumerable<DeliveredBasalSegment> delivered,
        IEnumerable<RateSegment> scheduled,
        IEnumerable<InsulinDose> doses)
    {
        _pieces = [.. Merge(
            [.. delivered.Where(d => d.End > d.Start).OrderBy(d => d.Start)],
            [.. scheduled.Where(s => s.End > s.Start).OrderBy(s => s.Start)])];
        _doses = [.. doses.Where(d => d.Units > 0).OrderBy(d => d.Mills)];
    }

    public IReadOnlyList<BasalPiece> Pieces => _pieces;

    /// <summary>Doses with insulin, by time.</summary>
    public IReadOnlyList<InsulinDose> Doses => _doses;

    public NetInsulin Over(long start, long end)
    {
        double algorithm = 0, manualTemp = 0, suspend = 0, inferred = 0, automaticBolus = 0, manualBolus = 0;
        long covered = 0;

        for (var i = FirstPieceEndingAfter(start); i < _pieces.Length && _pieces[i].Start < end; i++)
        {
            var piece = _pieces[i];
            if (!piece.HasDelta)
                continue;
            var ms = Math.Min(end, piece.End) - Math.Max(start, piece.Start);
            if (ms <= 0)
                continue;
            covered += ms;
            var units = (piece.Delivered!.Value - piece.Scheduled!.Value) * ms / HourMs;
            switch (piece.Origin)
            {
                case NetInsulinOrigin.AlgorithmTemp: algorithm += units; break;
                case NetInsulinOrigin.ManualTemp: manualTemp += units; break;
                case NetInsulinOrigin.Suspend: suspend += units; break;
                default: inferred += units; break;
            }
        }

        for (var i = FirstDoseAtOrAfter(start); i < _doses.Length && _doses[i].Mills < end; i++)
        {
            if (_doses[i].Automatic)
                automaticBolus += _doses[i].Units;
            else
                manualBolus += _doses[i].Units;
        }

        return new NetInsulin(algorithm, automaticBolus, manualTemp, suspend, inferred, manualBolus, covered);
    }

    public static NetInsulinOrigin OriginOf(BasalDeliveryOrigin origin) => origin switch
    {
        BasalDeliveryOrigin.Algorithm => NetInsulinOrigin.AlgorithmTemp,
        BasalDeliveryOrigin.Manual => NetInsulinOrigin.ManualTemp,
        BasalDeliveryOrigin.Suspended => NetInsulinOrigin.Suspend,
        _ => NetInsulinOrigin.Inferred,
    };

    /// <summary>
    /// The schedule a delivered rate is measured against: <paramref name="schedule"/>, scaled
    /// wherever an override with an insulin-needs scale factor was running. Where overrides overlap,
    /// the one that started last is in force.
    /// </summary>
    public static List<RateSegment> ScheduledBaseline(
        IEnumerable<BasalSegment> schedule, IEnumerable<ScaleFactorInterval> overrides)
    {
        var factors = overrides.Where(o => o.End > o.Start && o.Factor != 1).OrderBy(o => o.Start).ToList();
        var result = new List<RateSegment>();
        foreach (var segment in schedule.Where(s => s.EndMills > s.StartMills).OrderBy(s => s.StartMills))
        {
            var bounds = new SortedSet<long> { segment.StartMills, segment.EndMills };
            foreach (var factor in factors)
            {
                if (factor.Start > segment.StartMills && factor.Start < segment.EndMills) bounds.Add(factor.Start);
                if (factor.End > segment.StartMills && factor.End < segment.EndMills) bounds.Add(factor.End);
            }

            long? previous = null;
            foreach (var bound in bounds)
            {
                if (previous is { } pieceStart)
                {
                    var active = factors.LastOrDefault(f => f.Start <= pieceStart && f.End > pieceStart);
                    var scale = active.End > active.Start ? active.Factor : 1;
                    result.Add(new RateSegment(pieceStart, bound, segment.UnitsPerHour * scale));
                }
                previous = bound;
            }
        }
        return result;
    }

    /// <summary>
    /// Temp basals as delivered: each ends where the next from the same device starts
    /// (<see cref="StatisticsService.ClipOverlappingTempBasals"/>), with the time they cover and the
    /// part of it whose delivery is unknown, for <see cref="DeliveredFromSeries"/>.
    /// </summary>
    public static (List<TempBasal> Clipped, IntervalSet Recorded, IntervalSet Unknown) ClipTempBasals(
        IEnumerable<TempBasal> tempBasals)
    {
        var clipped = new List<TempBasal>();
        var recorded = new List<MillsInterval>();
        var unknown = new List<MillsInterval>();
        foreach (var (tb, effectiveEnd) in StatisticsService.ClipOverlappingTempBasals(tempBasals))
        {
            if (effectiveEnd <= tb.StartMills)
                continue;
            clipped.Add(new TempBasal
            {
                Id = tb.Id,
                StartTimestamp = tb.StartTimestamp,
                EndTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(effectiveEnd).UtcDateTime,
                Rate = tb.Rate,
                ScheduledRate = tb.ScheduledRate,
                Percent = tb.Percent,
                Origin = tb.Origin,
                DeviceId = tb.DeviceId,
                Device = tb.Device,
            });
            recorded.Add(new MillsInterval(tb.StartMills, effectiveEnd));
            if (tb.Rate is null && tb.Origin != TempBasalOrigin.Suspended)
                unknown.Add(new MillsInterval(tb.StartMills, effectiveEnd));
        }
        return (clipped, IntervalSet.Of(recorded), IntervalSet.Of(unknown));
    }

    /// <summary>
    /// Delivered basal from a step series in which each point holds until the next, as
    /// <c>IBasalSeriesBuilder</c> builds it from temp basals with schedule-filled gaps.
    /// </summary>
    /// <param name="end">Where the last point stops.</param>
    /// <param name="recorded">
    /// Time the temp basal records cover. A recorded point holds only within it, so a gap the series
    /// left unfilled (no schedule to fill it from) reads as unknown rather than as the temp before it.
    /// </param>
    /// <param name="unknown">
    /// Time whose delivery is unknown though a temp covered it: a relative temp with no rate. The
    /// series shows the schedule there; it is removed.
    /// </param>
    public static List<DeliveredBasalSegment> DeliveredFromSeries(
        IEnumerable<BasalPoint> series, long end, IntervalSet recorded, IntervalSet unknown)
    {
        var points = series.OrderBy(p => p.Timestamp).ToList();
        var result = new List<DeliveredBasalSegment>();
        for (var i = 0; i < points.Count; i++)
        {
            var start = points[i].Timestamp;
            var stop = i + 1 < points.Count ? points[i + 1].Timestamp : end;
            if (stop <= start)
                continue;

            var point = points[i];
            void Add(long pieceStart, long pieceEnd) =>
                unknown.ForEachGap(pieceStart, pieceEnd, (knownStart, knownEnd) =>
                    result.Add(new DeliveredBasalSegment(knownStart, knownEnd, point.Rate, point.Origin)));

            if (point.Origin == BasalDeliveryOrigin.Inferred)
                Add(start, stop);
            else
                recorded.ForEachOverlap(start, stop, Add);
        }
        return result;
    }

    private static IEnumerable<BasalPiece> Merge(DeliveredBasalSegment[] delivered, RateSegment[] scheduled)
    {
        var bounds = new SortedSet<long>();
        foreach (var d in delivered) { bounds.Add(d.Start); bounds.Add(d.End); }
        foreach (var s in scheduled) { bounds.Add(s.Start); bounds.Add(s.End); }

        int di = 0, si = 0;
        long? previous = null;
        foreach (var bound in bounds)
        {
            if (previous is { } start)
            {
                while (di < delivered.Length && delivered[di].End <= start) di++;
                while (si < scheduled.Length && scheduled[si].End <= start) si++;

                var d = di < delivered.Length && delivered[di].Start <= start ? delivered[di] : (DeliveredBasalSegment?)null;
                var s = si < scheduled.Length && scheduled[si].Start <= start ? scheduled[si] : (RateSegment?)null;
                if (d is not null || s is not null)
                    yield return new BasalPiece(
                        start, bound, d?.Rate, s?.Rate, d is { } known ? OriginOf(known.Origin) : null);
            }
            previous = bound;
        }
    }

    private int FirstPieceEndingAfter(long mills)
    {
        int lo = 0, hi = _pieces.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (_pieces[mid].End <= mills) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    private int FirstDoseAtOrAfter(long mills)
    {
        int lo = 0, hi = _doses.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (_doses[mid].Mills < mills) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}
