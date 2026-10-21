using System.Buffers;
using System.Text;
using System.Text.Json;
using Nocturne.Core.Models;
using Nocturne.Core.Models.LoopAnalysis;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Clinical.LoopAnalysis;

/// <summary>
/// One snapshot's prediction curves, mg/dL, indexed by <see cref="PredictionCurve"/>: value
/// <c>i</c> of a curve is <see cref="PredictionAccuracyCalculator.StepMinutes"/> × <c>i</c> minutes
/// after <see cref="StartMills"/>, up to the last horizon. A curve the snapshot did not carry is
/// null, and so is a value it left out.
/// </summary>
public sealed record PredictionSnapshot(long StartMills, AidAlgorithm Algorithm, double?[]?[] Curves);

/// <summary>Everything the Predictions tab is computed from, fetched once for every range it covers.</summary>
public sealed record PredictionAccuracyInputs
{
    public required TimeZoneInfo Zone { get; init; }

    /// <summary>Ordered by start.</summary>
    public IReadOnlyList<PredictionSnapshot> Snapshots { get; init; } = [];

    /// <summary>
    /// Ordered by time, reaching <see cref="PredictionAccuracyCalculator.ReadingsAfterRangeMs"/>
    /// past the range so a prediction made near its end is paired.
    /// </summary>
    public IReadOnlyList<GlucoseSample> Glucose { get; init; } = [];

    /// <summary>
    /// <see cref="StateSpanCategory.DataExclusion"/> time. A horizon landing in it is not paired
    /// and not counted unmatched: the readings there are known to be wrong, not missing.
    /// </summary>
    public IntervalSet GlucoseExclusions { get; init; } = IntervalSet.Empty;

    /// <summary>Carb entries of more than zero grams, ordered.</summary>
    public IReadOnlyList<long> CarbMills { get; init; } = [];

    /// <summary>Manual boluses of more than zero units, ordered.</summary>
    public IReadOnlyList<long> ManualBolusMills { get; init; } = [];

    public IntervalSet ExerciseOrOverride { get; init; } = IntervalSet.Empty;
}

/// <summary>
/// Pairs each prediction's value at fixed horizons with the sensor reading nearest the time it
/// predicted, and summarises the error by curve, horizon, local hour of day and context.
/// </summary>
/// <remarks>
/// A prediction counts where its start is in the analysed time. Where it does not reach a horizon
/// (the curve is shorter, or leaves the value out) that horizon is not a prediction at all.
/// </remarks>
public static class PredictionAccuracyCalculator
{
    public const int StepMinutes = 5;

    public static readonly IReadOnlyList<int> HorizonMinutes = [30, 60, 90, 120];

    /// <summary>The horizons whose bias is read as a settings signal; see <see cref="PredictionHorizonAccuracy"/>.</summary>
    public static readonly IReadOnlyList<int> SettingsSignalMinutes = [30, 60];

    public const long MatchToleranceMs = 150_000;

    /// <summary>How far back carbs or a manual bolus put a prediction in a post-meal or post-correction context.</summary>
    public const long ContextLookbackMs = 3 * LocalSlotBinning.HourMs;

    public static long ReadingsAfterRangeMs => HorizonMinutes[^1] * LocalSlotBinning.MinuteMs + MatchToleranceMs;

    private static readonly int CurveCount = Enum.GetValues<PredictionCurve>().Length;

    private static readonly PredictionContext[] AllContexts = Enum.GetValues<PredictionContext>();

    private static readonly PredictionContext[][] ContextsOf =
    [
        AllContexts,
        [PredictionContext.PostCorrection, PredictionContext.Fasting],
        [PredictionContext.PostCorrection, PredictionContext.Fasting],
        [PredictionContext.PostMeal],
        [PredictionContext.PostMeal],
    ];

    /// <summary>
    /// The curves of a stored snapshot. One carrying any of oref's curves is read as oref, whose
    /// default curve the decomposer fills with a copy of the IOB curve (Trio's it leaves empty), so
    /// its default is dropped rather than counted twice.
    /// </summary>
    /// <remarks>
    /// oref drops the trailing values of a curve that has levelled off, keeping at least 13 (7 of the
    /// zero-temp curve), so an oref curve that ends before the last horizon is read as holding its
    /// last value from there. Loop's curve is never shortened that way and is read as it is.
    /// </remarks>
    /// <returns>Null when the snapshot carries no readable curve.</returns>
    public static PredictionSnapshot? Parse(ApsPredictionPoint point)
    {
        var curves = new double?[]?[CurveCount];
        curves[(int)PredictionCurve.Iob] = ExtendFlatTail(ParseCurve(point.IobJson));
        curves[(int)PredictionCurve.ZeroTemp] = ExtendFlatTail(ParseCurve(point.ZtJson));
        curves[(int)PredictionCurve.Cob] = ExtendFlatTail(ParseCurve(point.CobJson));
        curves[(int)PredictionCurve.Uam] = ExtendFlatTail(ParseCurve(point.UamJson));
        if (curves.All(c => c is null))
            curves[(int)PredictionCurve.Default] = ParseCurve(point.DefaultJson);
        if (curves.All(c => c is null))
            return null;

        var start = new DateTimeOffset(DateTime.SpecifyKind(point.PredictedStart, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        return new PredictionSnapshot(start, point.AidAlgorithm, curves);
    }

    /// <summary>
    /// Reads a curve only as far as the last horizon. A 90-day range holds about 26,000 snapshots
    /// of up to four curves each, and deserialising them whole is most of the endpoint's time.
    /// </summary>
    private static double?[]? ParseCurve(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        var bytes = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(json.Length));
        try
        {
            var reader = new Utf8JsonReader(bytes.AsSpan(0, Encoding.UTF8.GetBytes(json, bytes)));
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
                return null;

            var values = new double?[LastIndex + 1];
            var count = 0;
            while (count < values.Length && reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
                    return null;
                values[count++] = reader.TokenType == JsonTokenType.Number && reader.TryGetDouble(out var value) ? value : null;
            }
            return count == 0 ? null : values[..count];
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    private static readonly int LastIndex = HorizonMinutes[^1] / StepMinutes;

    private static double?[]? ExtendFlatTail(double?[]? curve)
    {
        if (curve is null || curve.Length > LastIndex || curve[^1] is not { } last)
            return curve;
        var extended = new double?[LastIndex + 1];
        curve.CopyTo(extended, 0);
        Array.Fill(extended, last, curve.Length, extended.Length - curve.Length);
        return extended;
    }

    public static PredictionAccuracyRange Calculate(PredictionAccuracyInputs inputs, LoopAnalysisMask mask, long from, long to)
    {
        var horizons = HorizonMinutes.Count;
        var cells = new Cell[CurveCount][];
        var carried = new bool[CurveCount];
        var algorithms = new SortedSet<AidAlgorithm>();
        var predictions = 0;

        foreach (var snapshot in inputs.Snapshots)
        {
            var start = snapshot.StartMills;
            if (start < from || start >= to || !mask.Analysed.Contains(start))
                continue;

            predictions++;
            algorithms.Add(snapshot.Algorithm);
            var context = ContextAt(inputs, start);
            var local = LocalSlotBinning.LocalTime(start, inputs.Zone);
            var day = DateOnly.FromDateTime(local).DayNumber;

            for (var c = 0; c < CurveCount; c++)
            {
                if (snapshot.Curves[c] is not { } curve)
                    continue;
                carried[c] = true;
                if (Array.IndexOf(ContextsOf[c], context) < 0)
                    continue;

                var curveCells = cells[c] ??= NewCells(c, horizons);
                for (var h = 0; h < horizons; h++)
                {
                    var minutes = HorizonMinutes[h];
                    var index = minutes / StepMinutes;
                    if (index >= curve.Length || curve[index] is not { } predicted || !double.IsFinite(predicted))
                        continue;

                    var target = start + minutes * LocalSlotBinning.MinuteMs;
                    if (inputs.GlucoseExclusions.Contains(target))
                        continue;

                    var actual = NearestReading(inputs.Glucose, target);
                    curveCells[OverallCell].Add(h, predicted, actual, day);
                    curveCells[HourCell(local.Hour)].Add(h, predicted, actual, day);
                    curveCells[ContextCell(Array.IndexOf(ContextsOf[c], context))].Add(h, predicted, actual, day);
                }
            }
        }

        var result = new PredictionAccuracyRange
        {
            From = DateTimeOffset.FromUnixTimeMilliseconds(from).UtcDateTime,
            To = DateTimeOffset.FromUnixTimeMilliseconds(to).UtcDateTime,
            Predictions = predictions,
            Algorithms = [.. algorithms],
        };

        for (var c = 0; c < CurveCount; c++)
        {
            if (!carried[c])
                continue;
            var curveCells = cells[c] ?? NewCells(c, horizons);
            var contexts = ContextsOf[c];
            result.Curves.Add(new PredictionCurveAccuracy
            {
                Curve = (PredictionCurve)c,
                Contexts = [.. contexts],
                Horizons = curveCells[OverallCell].Summarise(),
                ByHour = [.. Enumerable.Range(0, HoursPerDay).Select(hour => new PredictionHourAccuracy
                {
                    Hour = hour,
                    Horizons = curveCells[HourCell(hour)].Summarise(),
                })],
                ByContext = [.. contexts.Select((context, i) => new PredictionContextAccuracy
                {
                    Context = context,
                    Horizons = curveCells[ContextCell(i)].Summarise(),
                })],
            });
        }

        return result;
    }

    /// <summary>The first context of <see cref="PredictionContext"/>'s order that applies at <paramref name="mills"/>.</summary>
    public static PredictionContext ContextAt(PredictionAccuracyInputs inputs, long mills)
    {
        if (inputs.ExerciseOrOverride.Contains(mills))
            return PredictionContext.ExerciseOrOverride;
        if (AnyWithin(inputs.CarbMills, mills - ContextLookbackMs, mills))
            return PredictionContext.PostMeal;
        if (AnyWithin(inputs.ManualBolusMills, mills - ContextLookbackMs, mills))
            return PredictionContext.PostCorrection;
        return PredictionContext.Fasting;
    }

    private static bool AnyWithin(IReadOnlyList<long> sorted, long from, long to)
    {
        var i = SortedSearch.FirstEndingAfter(sorted, from - 1, static m => m);
        return i < sorted.Count && sorted[i] <= to;
    }

    /// <summary>The reading nearest <paramref name="target"/> within <see cref="MatchToleranceMs"/>; the earlier on a tie.</summary>
    private static double? NearestReading(IReadOnlyList<GlucoseSample> glucose, long target)
    {
        var i = SortedSearch.FirstEndingAfter(glucose, target - MatchToleranceMs - 1, static g => g.Mills);
        double? best = null;
        var bestDistance = long.MaxValue;
        for (; i < glucose.Count && glucose[i].Mills <= target + MatchToleranceMs; i++)
        {
            var distance = Math.Abs(glucose[i].Mills - target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = glucose[i].Mgdl;
            }
        }
        return best;
    }

    private const int HoursPerDay = 24;
    private const int OverallCell = 0;

    private static int HourCell(int hour) => 1 + hour;

    private static int ContextCell(int index) => 1 + HoursPerDay + index;

    private static Cell[] NewCells(int curve, int horizons)
    {
        var cells = new Cell[ContextCell(ContextsOf[curve].Length)];
        for (var i = 0; i < cells.Length; i++)
            cells[i] = new Cell(horizons);
        return cells;
    }

    private sealed class Cell(int horizons)
    {
        private readonly List<double>[] _errors = [.. Enumerable.Range(0, horizons).Select(_ => new List<double>())];
        private readonly double[] _absolute = new double[horizons];
        private readonly double[] _relative = new double[horizons];
        private readonly int[] _unmatched = new int[horizons];
        private readonly HashSet<int>[] _days = [.. Enumerable.Range(0, horizons).Select(_ => new HashSet<int>())];

        public void Add(int horizon, double predicted, double? actual, int day)
        {
            if (actual is not { } mgdl)
            {
                _unmatched[horizon]++;
                return;
            }

            var error = predicted - mgdl;
            _errors[horizon].Add(error);
            _absolute[horizon] += Math.Abs(error);
            _relative[horizon] += Math.Abs(error) / mgdl;
            _days[horizon].Add(day);
        }

        public List<PredictionHorizonAccuracy> Summarise()
        {
            var result = new List<PredictionHorizonAccuracy>(_errors.Length);
            for (var h = 0; h < _errors.Length; h++)
            {
                var n = _errors[h].Count;
                result.Add(new PredictionHorizonAccuracy
                {
                    Minutes = HorizonMinutes[h],
                    Error = SlotSummary.Of(_errors[h], _days[h].Count),
                    Mae = n == 0 ? null : SlotSummary.Round(_absolute[h] / n),
                    Mard = n == 0 ? null : SlotSummary.Round(100 * _relative[h] / n),
                    Unmatched = _unmatched[h],
                    SettingsSignal = SettingsSignalMinutes.Contains(HorizonMinutes[h]),
                });
            }
            return result;
        }
    }
}
