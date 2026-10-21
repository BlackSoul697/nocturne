using Nocturne.API.Services.Profiles.Resolvers;
using Nocturne.Core.Clinical.LoopAnalysis;
using Nocturne.Core.Clinical.Schedules;
using Nocturne.Core.Clinical.Treatments;
using Nocturne.Core.Contracts.Analytics;
using Nocturne.Core.Contracts.Glucose;
using Nocturne.Core.Contracts.Profiles.Resolvers;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Core.Contracts.Treatments;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Basal;
using Nocturne.Core.Models.LoopAnalysis;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.Analytics;

/// <summary>
/// Fetches one window of everything Loop Analysis reads and hands it to the Core calculators.
/// </summary>
/// <remarks>
/// A primary and comparison range that touch or overlap are served from one fetch over both;
/// apart, each gets its own. Every collection is read uncapped, for the reason given on
/// <c>StatisticsController.FetchInsulinRecordsAsync</c>, and treatments are read from
/// <see cref="DailyProfileCalculator.ActingMs"/> before the window so IOB and COB at its start see
/// the doses still acting. Temp basals and APS snapshots are read to
/// <see cref="NetInsulinAboveSchedule.MaxInferredGapMs"/> past it, so a gap that straddles its end
/// is bounded by the evidence that closes it.
/// </remarks>
internal sealed class LoopAnalysisService(
    ISensorGlucoseRepository glucoseRepository,
    ICanonicalGlucoseService canonicalGlucose,
    IBolusRepository bolusRepository,
    ICarbIntakeRepository carbIntakeRepository,
    ITempBasalRepository tempBasalRepository,
    IApsSnapshotRepository apsSnapshotRepository,
    IStateSpanRepository stateSpanRepository,
    IPatientDeviceRepository patientDeviceRepository,
    ITargetRangeScheduleRepository targetRangeScheduleRepository,
    ITherapyTimelineResolver therapyTimelineResolver,
    ITherapySettingsResolver therapySettingsResolver,
    IActiveProfileResolver activeProfileResolver,
    IBasalSeriesBuilder basalSeriesBuilder,
    IBasalSegmentService basalSegmentService,
    IIobCalculator iobCalculator,
    ICobCalculator cobCalculator
) : ILoopAnalysisService
{
    private const long TickMs = DailyProfileCalculator.SlotMinutes * LocalSlotBinning.MinuteMs;

    private static readonly StateSpanCategory[] MaskCategories =
    [
        StateSpanCategory.Override,
        StateSpanCategory.TemporaryTarget,
        StateSpanCategory.Exercise,
        StateSpanCategory.Illness,
        StateSpanCategory.DataExclusion,
        StateSpanCategory.PumpMode,
    ];

    public async Task<DailyProfileResponse> GetDailyProfileAsync(
        LoopAnalysisFilter filter, TimeZoneInfo zone, CancellationToken ct = default)
    {
        var (primary, comparison) = await ForRangesAsync(
            filter,
            window => LoadAsync(window, zone, ct),
            (loaded, window) => DailyProfileCalculator.Calculate(
                loaded.Inputs, Mask(filter, window, zone, loaded.Sources), window.From, window.To));
        return new DailyProfileResponse { Primary = primary, Comparison = comparison };
    }

    public async Task<PredictionAccuracyResponse> GetPredictionAccuracyAsync(
        LoopAnalysisFilter filter, TimeZoneInfo zone, CancellationToken ct = default)
    {
        var (primary, comparison) = await ForRangesAsync(
            filter,
            window => LoadPredictionsAsync(window, zone, filter.Masks.ProfileName is not null, ct),
            (loaded, window) => PredictionAccuracyCalculator.Calculate(
                loaded.Inputs, Mask(filter, window, zone, loaded.Sources), window.From, window.To));
        return new PredictionAccuracyResponse { Primary = primary, Comparison = comparison };
    }

    /// <summary>
    /// Computes the primary range and the comparison: from one load over both where they touch or
    /// overlap, else one load each.
    /// </summary>
    private static async Task<(TRange Primary, TRange? Comparison)> ForRangesAsync<TLoaded, TRange>(
        LoopAnalysisFilter filter, Func<Window, Task<TLoaded>> load, Func<TLoaded, Window, TRange> calculate)
        where TRange : class
    {
        var primary = Window.Of(filter.From, filter.To);
        var comparison = filter.CompareFrom is { } compareFrom && filter.CompareTo is { } compareTo
            ? Window.Of(compareFrom, compareTo)
            : (Window?)null;

        if (comparison is { } other && primary.Touches(other))
        {
            var both = await load(primary.Hull(other));
            return (calculate(both, primary), calculate(both, other));
        }

        var first = calculate(await load(primary), primary);
        return (first, comparison is { } separate ? calculate(await load(separate), separate) : null);
    }

    private static LoopAnalysisMask Mask(
        LoopAnalysisFilter filter, Window window, TimeZoneInfo zone, LoopAnalysisMaskSources sources) =>
        LoopAnalysisMask.Build(filter.Masks, window.From, window.To, zone, sources);

    private sealed record Loaded(DailyProfileInputs Inputs, LoopAnalysisMaskSources Sources);

    private sealed record LoadedPredictions(PredictionAccuracyInputs Inputs, LoopAnalysisMaskSources Sources);

    private async Task<Loaded> LoadAsync(Window window, TimeZoneInfo zone, CancellationToken ct)
    {
        var from = window.From;
        var to = window.To;
        var fromUtc = ToUtc(from);
        var toUtc = ToUtc(to);
        var lookbackUtc = ToUtc(from - DailyProfileCalculator.ActingMs);

        var glucoseTask = glucoseRepository.GetForAnalyticsAsync(fromUtc, toUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var bolusTask = bolusRepository.GetAsync(lookbackUtc, toUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var carbTask = carbIntakeRepository.GetAsync(lookbackUtc, toUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var evidenceEndUtc = ToUtc(to + NetInsulinAboveSchedule.MaxInferredGapMs);
        var tempBasalTask = tempBasalRepository.GetAsync(lookbackUtc, evidenceEndUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var apsTask = apsSnapshotRepository.GetIobCobPointsAsync(lookbackUtc, evidenceEndUtc, ct);
        var spanTask = stateSpanRepository.GetByCategories(MaskCategories, fromUtc, toUtc, ct);
        var deviceTask = patientDeviceRepository.GetByDateRangeAsync(fromUtc, toUtc, ct);
        await Task.WhenAll(glucoseTask, bolusTask, carbTask, tempBasalTask, apsTask, spanTask, deviceTask);

        var glucose = await canonicalGlucose.SelectAsync([.. await glucoseTask], ct);
        var boluses = (await bolusTask).ToList();
        var carbs = (await carbTask).ToList();
        var tempBasals = (await tempBasalTask).ToList();
        var apsPoints = await apsTask;
        var spans = await spanTask;
        var snapshotMills = apsPoints
            .Select(p => ToMills(DateTime.SpecifyKind(p.Timestamp, DateTimeKind.Utc)))
            .ToList();

        var hasTherapy = await therapySettingsResolver.HasDataAsync(ct);
        var profiles = await activeProfileResolver.GetProfileSpansForRangeAsync(from, to, ct);
        var tickStart = from - from % TickMs;
        var timeline = await therapyTimelineResolver.BuildAsync(tickStart, to + 1, ct: ct);

        var overrides = SpansOf(spans, StateSpanCategory.Override, to);
        var scheduled = hasTherapy
            ? NetInsulinAboveSchedule.ScheduledBaseline(
                await ScheduleAsync(from, to, ct),
                overrides.Select(o => new ScaleFactorInterval(o.Interval.Start, o.Interval.End,(double)(o.Span.Metadata?.OverrideScaleFactor() ?? 1m))))
            : [];

        var (clipped, evidence) = NetInsulinAboveSchedule.ClipTempBasals(tempBasals, snapshotMills);
        var series = await basalSeriesBuilder.BuildAsync(
            clipped, tickStart, to, timeline.SnapshotAt(from).BasalRateAt(from), timeline, ct);
        var delivered = NetInsulinAboveSchedule.DeliveredFromSeries(series, to, evidence);

        var doses = boluses
            .Where(b => b.Mills >= from && b.Mills < to)
            .Select(b => new InsulinDose(b.Mills, b.Insulin, b.Kind == BolusKind.Algorithm));

        var iobCob = IobCobSeries.Build(
            iobCalculator, cobCalculator, boluses, carbs, tempBasals, timeline, apsPoints,
            tickStart, to - 1, TickMs, ct);
        var temporaryTargets = ActiveTemporaryTargets(spans, to);

        var profileIntervals = profiles
            .Select(p => new ProfileInterval(p.ProfileName, p.StartMills, p.EndMills ?? to))
            .ToList();

        var inputs = new DailyProfileInputs
        {
            Zone = zone,
            Glucose = [.. glucose.Select(g => new GlucoseSample(g.Mills, g.Mgdl))],
            Insulin = new NetInsulinAboveSchedule(delivered, scheduled, doses),
            Carbs = [.. carbs.Where(c => c.Mills >= from && c.Mills < to).Select(c => new CarbEvent(c.Mills, c.Carbs))],
            IobCob = iobCob,
            IobCobIntervalMs = TickMs,
            CalculatedIobCobTime = DailyProfileCalculator.CalculableIobCobTime(
                evidence,
                boluses.Where(b => b.Insulin > 0).Select(b => b.Mills)
                    .Concat(carbs.Where(c => c.Carbs > 0).Select(c => c.Mills))),
            TargetAt = await TargetRangesAsync(hasTherapy ? profiles : [], temporaryTargets, zone, to, ct),
            Profiles = profileIntervals,
        };

        return new Loaded(inputs, MaskSources(spans, await deviceTask, snapshotMills, profileIntervals, zone, to));
    }

    /// <summary>
    /// Every prediction made in the window, the readings up to
    /// <see cref="PredictionAccuracyCalculator.ReadingsAfterRangeMs"/> past it that the last of them
    /// are paired with, and the carbs and manual boluses
    /// <see cref="PredictionAccuracyCalculator.ContextLookbackMs"/> before it that set the context.
    /// Snapshots with predictions are the AID evidence the open-loop mask weighs.
    /// </summary>
    private async Task<LoadedPredictions> LoadPredictionsAsync(
        Window window, TimeZoneInfo zone, bool needsProfiles, CancellationToken ct)
    {
        var (from, to) = (window.From, window.To);
        var fromUtc = ToUtc(from);
        var toUtc = ToUtc(to);
        var readingsEndUtc = ToUtc(to + PredictionAccuracyCalculator.ReadingsAfterRangeMs);
        var lookbackUtc = ToUtc(from - PredictionAccuracyCalculator.ContextLookbackMs);

        var predictionTask = apsSnapshotRepository.GetPredictionsAsync(fromUtc, toUtc, ct);
        var glucoseTask = glucoseRepository.GetForAnalyticsAsync(fromUtc, readingsEndUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var bolusTask = bolusRepository.GetAsync(lookbackUtc, toUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var carbTask = carbIntakeRepository.GetAsync(lookbackUtc, toUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var spanTask = stateSpanRepository.GetByCategories(MaskCategories, fromUtc, readingsEndUtc, ct);
        var deviceTask = patientDeviceRepository.GetByDateRangeAsync(fromUtc, toUtc, ct);
        await Task.WhenAll(predictionTask, glucoseTask, bolusTask, carbTask, spanTask, deviceTask);

        var snapshots = (await predictionTask)
            .Select(PredictionAccuracyCalculator.Parse)
            .OfType<PredictionSnapshot>()
            .ToList();
        var glucose = await canonicalGlucose.SelectAsync([.. await glucoseTask], ct);
        var spans = await spanTask;
        var profiles = needsProfiles
            ? (await activeProfileResolver.GetProfileSpansForRangeAsync(from, to, ct))
                .Select(p => new ProfileInterval(p.ProfileName, p.StartMills, p.EndMills ?? to))
                .ToList()
            : [];
        var sources = MaskSources(spans, await deviceTask, [.. snapshots.Select(s => s.StartMills)], profiles, zone, to);

        var inputs = new PredictionAccuracyInputs
        {
            Zone = zone,
            Snapshots = snapshots,
            Glucose = [.. glucose.Select(g => new GlucoseSample(g.Mills, g.Mgdl)).Where(g => g.Mgdl > 0).OrderBy(g => g.Mills)],
            GlucoseExclusions = IntervalSet.Of(sources.DataExclusions),
            CarbMills = [.. (await carbTask).Where(c => c.Carbs > 0).Select(c => c.Mills).Order()],
            ManualBolusMills = [.. (await bolusTask).Where(b => b.Kind != BolusKind.Algorithm && b.Insulin > 0).Select(b => b.Mills).Order()],
            ExerciseOrOverride = IntervalSet.Of(sources.Overrides.Concat(sources.Exercise)),
        };
        return new LoadedPredictions(inputs, sources);
    }

    private static LoopAnalysisMaskSources MaskSources(
        Dictionary<StateSpanCategory, List<StateSpan>> spans,
        IEnumerable<PatientDevice> devices,
        IReadOnlyList<long> snapshotMills,
        IReadOnlyList<ProfileInterval> profiles,
        TimeZoneInfo zone,
        long to) => new()
    {
        Overrides = [.. SpansOf(spans, StateSpanCategory.Override, to).Select(s => s.Interval)],
        TemporaryTargets = [.. ActiveTemporaryTargets(spans, to).Select(s => s.Interval)],
        Exercise = [.. SpansOf(spans, StateSpanCategory.Exercise, to).Select(s => s.Interval)],
        Illness = [.. SpansOf(spans, StateSpanCategory.Illness, to).Select(s => s.Interval)],
        DataExclusions = [.. SpansOf(spans, StateSpanCategory.DataExclusion, to).Select(s => s.Interval)],
        OpenLoop = OpenLoop(spans, devices, snapshotMills, zone, to).Intervals,
        Profiles = profiles,
    };

    private static List<(StateSpan Span, MillsInterval Interval)> ActiveTemporaryTargets(
        Dictionary<StateSpanCategory, List<StateSpan>> spans, long to) =>
        [.. SpansOf(spans, StateSpanCategory.TemporaryTarget, to)
            .Where(s => s.Span.State != nameof(TemporaryTargetState.Cancelled))];

    private async Task<List<BasalSegment>> ScheduleAsync(long from, long to, CancellationToken ct)
    {
        var segments = new List<BasalSegment>();
        await foreach (var segment in basalSegmentService.GetSegmentsAsync(from, to, ct))
            segments.Add(segment);
        return segments;
    }

    /// <summary>
    /// The target range in force at an instant: a running temporary target, read in mg/dL as
    /// <see cref="TemporaryTargetUnits"/> reads it, else the schedule. The schedule is the
    /// activation's own targets when it carries them, else the profile's stored version in force, as
    /// <see cref="ActiveSchedule"/> reads it for one instant, from versions read once per profile.
    /// </summary>
    private async Task<Func<long, (double Low, double High)?>> TargetRangesAsync(
        IReadOnlyList<ProfileSpan> profiles,
        IEnumerable<(StateSpan Span, MillsInterval Interval)> temporaryTargets,
        TimeZoneInfo zone,
        long to,
        CancellationToken ct)
    {
        var versions = new Dictionary<string, TargetRangeSchedule[]>(StringComparer.Ordinal);
        foreach (var name in profiles.Select(p => p.ProfileName).Distinct(StringComparer.Ordinal))
            versions[name] = [.. (await targetRangeScheduleRepository.GetByProfileNameAsync(name, ct))
                .OrderByDescending(v => v.Timestamp)];

        var temporary = StepTimeline<(double Low, double High)>.LatestStartedWins(temporaryTargets
            .Select(t => (t.Interval, Range: TemporaryTargetRange(t.Span)))
            .Where(t => t.Range is not null)
            .Select(t => new Step<(double Low, double High)>(t.Interval.Start, t.Interval.End, t.Range!.Value)));
        var scheduled = StepTimeline<ProfileSpan>.LatestStartedWins(
            profiles.Select(p => new Step<ProfileSpan>(p.StartMills, p.EndMills ?? to, p)));

        return mills =>
        {
            if (temporary.TryAt(mills, out var target))
                return target;
            if (!scheduled.TryAt(mills, out var span))
                return null;
            var entries = span.Schedules?.Targets
                ?? ProfileVersionCache.ActiveAt(
                    versions[span.ProfileName], DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime)?.Entries;
            if (entries is null)
                return null;
            var shifted = mills + (span.Adjustment?.TimeshiftMs ?? 0);
            return ScheduleResolution.FindRangeAtTime(entries, ScheduleTimeHelper.GetSecondsFromMidnight(shifted, zone));
        };
    }

    private static (double Low, double High)? TemporaryTargetRange(StateSpan span)
    {
        var (top, bottom) = TemporaryTargetUnits.ToMgdl(
            (double?)span.Metadata.TryReadDecimal("targetTop"),
            (double?)span.Metadata.TryReadDecimal("targetBottom"),
            span.Metadata.TryReadString("units"));
        return (bottom ?? top, top ?? bottom) is (double low, double high) ? (low, high) : null;
    }

    /// <summary>
    /// <see cref="LoopAnalysisMask.OpenLoop"/> from the pump mode spans and the registered pumps. A
    /// pump whose algorithm is <see cref="AidAlgorithm.Unknown"/> is neither: it says nothing about
    /// whether the loop was closed.
    /// </summary>
    private static IntervalSet OpenLoop(
        Dictionary<StateSpanCategory, List<StateSpan>> spans,
        IEnumerable<PatientDevice> devices,
        IReadOnlyList<long> snapshotMills,
        TimeZoneInfo zone,
        long to)
    {
        var pumps = devices
            .Where(d => d.DeviceCategory == DeviceCategory.InsulinPump && d.AidAlgorithm is { } algorithm && algorithm != AidAlgorithm.Unknown)
            .ToList();
        MillsInterval Window(PatientDevice pump) => LoopAnalysisMask.DeviceWindow(pump.StartDate, pump.EndDate, zone, to);

        return LoopAnalysisMask.OpenLoop(
            SpansOf(spans, StateSpanCategory.PumpMode, to)
                .Where(s => s.Span.State is nameof(PumpModeState.Manual) or nameof(PumpModeState.Off))
                .Select(s => s.Interval),
            pumps.Where(p => p.AidAlgorithm == AidAlgorithm.None).Select(Window),
            pumps.Where(p => p.AidAlgorithm != AidAlgorithm.None).Select(Window),
            snapshotMills);
    }

    private static List<(StateSpan Span, MillsInterval Interval)> SpansOf(
        Dictionary<StateSpanCategory, List<StateSpan>> spans, StateSpanCategory category, long to) =>
        spans.TryGetValue(category, out var list)
            ? [.. list.Select(s => (s, new MillsInterval(s.StartMills, s.EndMills ?? to)))]
            : [];

    private static DateTime ToUtc(long mills) => DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime;

    private static long ToMills(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private readonly record struct Window(long From, long To)
    {
        public static Window Of(DateTime from, DateTime to) =>
            to > from
                ? new(ToMills(from), ToMills(to))
                : throw new ArgumentOutOfRangeException(nameof(to), "A Loop Analysis range must end after it starts.");

        public bool Touches(Window other) => From <= other.To && other.From <= To;

        public Window Hull(Window other) => new(Math.Min(From, other.From), Math.Max(To, other.To));
    }
}
