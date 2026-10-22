using Nocturne.Core.Clinical.LoopAnalysis;
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
/// Each tab's load and calculation is in its own part (<c>LoopAnalysisService.&lt;Tab&gt;.cs</c>);
/// this one holds what the tabs share.
/// </summary>
/// <remarks>
/// A primary and comparison range that touch or overlap are served from one fetch over both;
/// apart, each gets its own. Every collection is read uncapped, for the reason given on
/// <c>StatisticsController.FetchInsulinRecordsAsync</c>, and treatments are read from
/// <see cref="DailyProfileCalculator.ActingMs"/> before the window so IOB and COB at its start see
/// the doses still acting. Temp basals are read to
/// <see cref="NetInsulinAboveSchedule.MaxInferredGapMs"/> past it, so a gap that straddles its end
/// is bounded by the record that closes it.
/// </remarks>
internal sealed partial class LoopAnalysisService(
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
    ICobCalculator cobCalculator,
    ITherapySettingsRepository therapySettingsRepository,
    AidSystemMetricsReader aidSystemMetrics,
    TimeProvider timeProvider
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

    /// <summary>The reads the Daily profile and Loop health tabs share, issued together.</summary>
    private sealed record Common(
        IReadOnlyList<SensorGlucose> Glucose,
        List<Bolus> Boluses,
        List<TempBasal> TempBasals,
        Dictionary<StateSpanCategory, List<StateSpan>> Spans,
        IReadOnlyList<PatientDevice> Devices,
        IReadOnlyList<ProfileSpan> Profiles,
        List<ProfileInterval> ProfileIntervals);

    private async Task<Common> LoadCommonAsync(Window window, CancellationToken ct)
    {
        var from = window.From;
        var to = window.To;
        var fromUtc = ToUtc(from);
        var toUtc = ToUtc(to);
        var lookbackUtc = ToUtc(from - DailyProfileCalculator.ActingMs);

        var glucoseTask = glucoseRepository.GetForAnalyticsAsync(fromUtc, toUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var bolusTask = bolusRepository.GetAsync(lookbackUtc, toUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var tempBasalTask = tempBasalRepository.GetAsync(
            lookbackUtc, ToUtc(to + NetInsulinAboveSchedule.MaxInferredGapMs), null, null, int.MaxValue, descending: false, ct: ct);
        var spanTask = stateSpanRepository.GetByCategories(MaskCategories, fromUtc, toUtc, ct);
        var deviceTask = patientDeviceRepository.GetByDateRangeAsync(fromUtc, toUtc, ct);
        await Task.WhenAll(glucoseTask, bolusTask, tempBasalTask, spanTask, deviceTask);

        var profiles = await activeProfileResolver.GetProfileSpansForRangeAsync(from, to, ct);
        return new Common(
            await canonicalGlucose.SelectAsync([.. await glucoseTask], ct),
            [.. await bolusTask],
            [.. await tempBasalTask],
            await spanTask,
            [.. await deviceTask],
            profiles,
            [.. profiles.Select(p => new ProfileInterval(p.ProfileName, p.StartMills, p.EndMills ?? to))]);
    }

    private sealed record Delivery(
        NetInsulinAboveSchedule Insulin,
        BasalEvidence Evidence,
        IReadOnlyList<long> TempStarts,
        TherapyTimeline Timeline,
        long TickStart,
        bool HasTherapy);

    /// <summary>
    /// Basal delivered against the schedule (scaled by any override) and every bolus in the window,
    /// as <see cref="NetInsulinAboveSchedule"/> defines them.
    /// </summary>
    private async Task<Delivery> DeliveryAsync(Common common, Window window, CancellationToken ct)
    {
        var from = window.From;
        var to = window.To;
        var hasTherapy = await therapySettingsResolver.HasDataAsync(ct);
        var tickStart = from - from % TickMs;
        var timeline = await therapyTimelineResolver.BuildAsync(tickStart, to + 1, ct: ct);

        var scheduled = hasTherapy
            ? NetInsulinAboveSchedule.ScheduledBaseline(
                await ScheduleAsync(from, to, ct),
                SpansOf(common.Spans, StateSpanCategory.Override, to)
                    .Select(o => new ScaleFactorInterval(o.Interval.Start, o.Interval.End, (double)(o.Span.Metadata?.OverrideScaleFactor() ?? 1m))))
            : [];

        var (clipped, evidence) = NetInsulinAboveSchedule.ClipTempBasals(common.TempBasals);
        var series = await basalSeriesBuilder.BuildAsync(
            clipped, tickStart, to, timeline.SnapshotAt(from).BasalRateAt(from), timeline, ct);
        var delivered = NetInsulinAboveSchedule.DeliveredFromSeries(series, to, evidence);

        var doses = common.Boluses
            .Where(b => b.Mills >= from && b.Mills < to)
            .Select(b => new InsulinDose(b.Mills, b.Insulin, b.Kind == BolusKind.Algorithm));

        return new Delivery(
            new NetInsulinAboveSchedule(delivered, scheduled, doses),
            evidence,
            [.. clipped.Select(t => t.StartMills).Order()],
            timeline,
            tickStart,
            hasTherapy);
    }

    private async Task<List<BasalSegment>> ScheduleAsync(long from, long to, CancellationToken ct)
    {
        var segments = new List<BasalSegment>();
        await foreach (var segment in basalSegmentService.GetSegmentsAsync(from, to, ct))
            segments.Add(segment);
        return segments;
    }

    /// <summary><see cref="MaskSources"/> over <paramref name="common"/>'s spans, devices and profiles.</summary>
    private static LoopAnalysisMaskSources Sources(
        Common common, IReadOnlyList<long> snapshotMills, TimeZoneInfo zone, long openEnd) =>
        MaskSources(common.Spans, common.Devices, snapshotMills, common.ProfileIntervals, zone, openEnd);

    /// <param name="openEnd">Where a span still open ends: the end of the time its readers look at.</param>
    private static LoopAnalysisMaskSources MaskSources(
        Dictionary<StateSpanCategory, List<StateSpan>> spans,
        IEnumerable<PatientDevice> devices,
        IReadOnlyList<long> snapshotMills,
        IReadOnlyList<ProfileInterval> profiles,
        TimeZoneInfo zone,
        long openEnd) => new()
    {
        Overrides = [.. SpansOf(spans, StateSpanCategory.Override, openEnd).Select(s => s.Interval)],
        TemporaryTargets = [.. ActiveTemporaryTargets(spans, openEnd).Select(s => s.Interval)],
        Exercise = [.. SpansOf(spans, StateSpanCategory.Exercise, openEnd).Select(s => s.Interval)],
        Illness = [.. SpansOf(spans, StateSpanCategory.Illness, openEnd).Select(s => s.Interval)],
        DataExclusions = [.. SpansOf(spans, StateSpanCategory.DataExclusion, openEnd).Select(s => s.Interval)],
        OpenLoop = OpenLoop(spans, devices, snapshotMills, zone, openEnd).Intervals,
        Profiles = profiles,
    };

    private static List<(StateSpan Span, MillsInterval Interval)> ActiveTemporaryTargets(
        Dictionary<StateSpanCategory, List<StateSpan>> spans, long to) =>
        [.. SpansOf(spans, StateSpanCategory.TemporaryTarget, to)
            .Where(s => s.Span.State != nameof(TemporaryTargetState.Cancelled))];

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
