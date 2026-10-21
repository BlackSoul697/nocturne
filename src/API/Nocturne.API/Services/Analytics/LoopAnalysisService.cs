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
/// Fetches one window of everything a Loop Analysis tab reads and hands it to the Core calculators.
/// </summary>
/// <remarks>
/// A primary and comparison range that touch or overlap are served from one fetch over both;
/// apart, each gets its own. Every collection is read uncapped, for the reason given on
/// <c>StatisticsController.FetchInsulinRecordsAsync</c>, and treatments and loop cycles are read
/// from <see cref="DailyProfileCalculator.ActingMs"/> before the window so IOB and COB at its start
/// see the doses still acting and a gap or temp that straddles it is bounded. Temp basals and loop
/// cycles are read to <see cref="NetInsulinAboveSchedule.MaxInferredGapMs"/> past it, so a gap that
/// straddles its end is bounded by the evidence that closes it.
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
    ITherapySettingsRepository therapySettingsRepository,
    ITherapyTimelineResolver therapyTimelineResolver,
    ITherapySettingsResolver therapySettingsResolver,
    IActiveProfileResolver activeProfileResolver,
    IBasalSeriesBuilder basalSeriesBuilder,
    IBasalSegmentService basalSegmentService,
    IIobCalculator iobCalculator,
    ICobCalculator cobCalculator,
    AidSystemMetricsReader aidSystemMetrics,
    TenantClockResolver tenantClock
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

    public async Task<DailyProfileResponse> GetDailyProfileAsync(LoopAnalysisFilter filter, CancellationToken ct = default)
    {
        var (primary, comparison, reason) = await ComputeAsync(
            filter,
            LoadDailyProfileAsync,
            (loaded, window) => DailyProfileCalculator.Calculate(
                loaded.Inputs, Mask(filter, window, loaded.Inputs.Zone, loaded.Sources), window.From, window.To),
            window => new DailyProfileRange
            {
                From = ToUtc(window.From),
                To = ToUtc(window.To),
                SlotMinutes = DailyProfileCalculator.SlotMinutes,
            },
            ct);
        return new DailyProfileResponse { Primary = primary, Comparison = comparison, TimeZoneUnavailableReason = reason };
    }

    public async Task<LoopHealthResponse> GetLoopHealthAsync(LoopAnalysisFilter filter, CancellationToken ct = default)
    {
        var (primary, comparison, reason) = await ComputeAsync(
            filter,
            LoadLoopHealthAsync,
            (loaded, window) => CalculateLoopHealth(loaded, filter, window),
            window => new LoopHealthRange { From = ToUtc(window.From), To = ToUtc(window.To) },
            ct);
        return new LoopHealthResponse { Primary = primary, Comparison = comparison, TimeZoneUnavailableReason = reason };
    }

    /// <summary>
    /// Resolves the tenant's zone and computes the primary and comparison ranges, from one fetch
    /// where they touch. A range a history clamp has emptied has nothing to fetch.
    /// </summary>
    private async Task<(TRange Primary, TRange? Comparison, TimeZoneUnavailableReason? Reason)> ComputeAsync<TLoaded, TRange>(
        LoopAnalysisFilter filter,
        Func<Window, TimeZoneInfo, CancellationToken, Task<TLoaded>> load,
        Func<TLoaded, Window, TRange> calculate,
        Func<Window, TRange> empty,
        CancellationToken ct)
        where TRange : class
    {
        var primary = Window.Of(filter.From, filter.To);
        var comparison = filter.CompareFrom is { } compareFrom && filter.CompareTo is { } compareTo
            ? Window.Of(compareFrom, compareTo)
            : (Window?)null;

        var (zone, reason) = await tenantClock.ResolveAsync(ct);
        if (zone is null)
            return (empty(primary), comparison is { } unzoned ? empty(unzoned) : null, reason);

        if (comparison is { IsEmpty: false } other && !primary.IsEmpty && primary.Touches(other))
        {
            var both = await load(primary.Hull(other), zone, ct);
            return (calculate(both, primary), calculate(both, other), reason);
        }

        async Task<TRange> One(Window window) =>
            window.IsEmpty ? empty(window) : calculate(await load(window, zone, ct), window);

        return (await One(primary), comparison is { } separate ? await One(separate) : null, reason);
    }

    private static LoopAnalysisMask Mask(LoopAnalysisFilter filter, Window window, TimeZoneInfo zone, LoopAnalysisMaskSources sources) =>
        LoopAnalysisMask.Build(filter, window.From, window.To, zone, sources);

    private sealed record DailyProfileLoaded(DailyProfileInputs Inputs, LoopAnalysisMaskSources Sources);

    private async Task<DailyProfileLoaded> LoadDailyProfileAsync(Window window, TimeZoneInfo zone, CancellationToken ct)
    {
        var from = window.From;
        var to = window.To;
        var lookbackUtc = ToUtc(from - DailyProfileCalculator.ActingMs);
        var toUtc = ToUtc(to);

        var carbTask = carbIntakeRepository.GetAsync(lookbackUtc, toUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var apsTask = apsSnapshotRepository.GetIobCobPointsAsync(lookbackUtc, ToUtc(to + NetInsulinAboveSchedule.MaxInferredGapMs), ct);
        var commonTask = LoadCommonAsync(window, ct);
        await Task.WhenAll(carbTask, apsTask, commonTask);

        var carbs = (await carbTask).ToList();
        var apsPoints = await apsTask;
        var snapshotMills = apsPoints
            .Select(p => ToMills(DateTime.SpecifyKind(p.Timestamp, DateTimeKind.Utc)))
            .ToList();
        var common = await commonTask;
        var delivery = await DeliveryAsync(common, window, snapshotMills, ct);

        var iobCob = IobCobSeries.Build(
            iobCalculator, cobCalculator, common.Boluses, carbs, common.TempBasals, delivery.Timeline, apsPoints,
            delivery.TickStart, to - 1, TickMs, ct);
        var temporaryTargets = TemporaryTargets(common.Spans, to);

        var inputs = new DailyProfileInputs
        {
            Zone = zone,
            Glucose = [.. common.Glucose.Select(g => new GlucoseSample(g.Mills, g.Mgdl))],
            Insulin = delivery.Insulin,
            Carbs = [.. carbs.Where(c => c.Mills >= from && c.Mills < to).Select(c => new CarbEvent(c.Mills, c.Carbs))],
            IobCob = iobCob,
            IobCobIntervalMs = TickMs,
            CalculatedIobCobTime = DailyProfileCalculator.CalculableIobCobTime(
                delivery.Evidence,
                common.Boluses.Where(b => b.Insulin > 0).Select(b => b.Mills)
                    .Concat(carbs.Where(c => c.Carbs > 0).Select(c => c.Mills))),
            TargetAt = await TargetRangesAsync(delivery.HasTherapy ? common.Profiles : [], temporaryTargets, zone, to, ct),
            Profiles = common.ProfileIntervals,
        };

        return new DailyProfileLoaded(inputs, Sources(common, snapshotMills, zone, to));
    }

    private sealed record LoopHealthLoaded(
        LoopHealthInputs Inputs,
        LoopAnalysisMaskSources Sources,
        IReadOnlyList<PatientDevice> Devices,
        IReadOnlyList<TempBasal> TempBasals);

    private async Task<LoopHealthLoaded> LoadLoopHealthAsync(Window window, TimeZoneInfo zone, CancellationToken ct)
    {
        var to = window.To;
        var cyclesTask = apsSnapshotRepository.GetLoopCyclesAsync(
            ToUtc(window.From - DailyProfileCalculator.ActingMs), ToUtc(to + NetInsulinAboveSchedule.MaxInferredGapMs), ct);
        var commonTask = LoadCommonAsync(window, ct);
        await Task.WhenAll(cyclesTask, commonTask);

        var cycles = await cyclesTask;
        var snapshotMills = cycles.Select(c => c.Mills).ToList();
        var common = await commonTask;
        var delivery = await DeliveryAsync(common, window, snapshotMills, ct);

        var inputs = new LoopHealthInputs
        {
            Zone = zone,
            Cycles = cycles,
            Insulin = delivery.Insulin,
            Evidence = delivery.Evidence,
            Scheduled = delivery.Scheduled,
            Glucose = common.Glucose,
            LoopMaximumBasalRate = cycles.Any(c => c.AidAlgorithm == AidAlgorithm.Loop)
                ? await LoopMaximumBasalRateAsync(common.Profiles, to, ct)
                : null,
        };

        return new LoopHealthLoaded(inputs, Sources(common, snapshotMills, zone, to), common.Devices, common.TempBasals);
    }

    /// <summary>
    /// <see cref="LoopHealthCalculator"/> over the window, with the overall activity percentages from
    /// <see cref="AidSystemMetricsReader"/> over the same records, unmasked.
    /// </summary>
    private LoopHealthRange CalculateLoopHealth(LoopHealthLoaded loaded, LoopAnalysisFilter filter, Window window)
    {
        var inputs = loaded.Inputs;
        var range = LoopHealthCalculator.Calculate(
            inputs, Mask(filter, window, inputs.Zone, loaded.Sources), window.From, window.To);

        var start = ToUtc(window.From);
        var end = ToUtc(window.To);
        var overall = aidSystemMetrics.Calculate(
            new AidSystemMetricsRecords(
                loaded.Devices,
                [.. inputs.Cycles.Where(c => c.Timestamp >= start && c.Timestamp < end)],
                [.. loaded.TempBasals.Where(t => t.StartTimestamp < end && (t.EndTimestamp ?? t.StartTimestamp) >= start)],
                [],
                [.. inputs.Glucose.Where(g => g.Mills >= window.From && g.Mills < window.To)],
                null,
                null),
            start,
            end);
        range.Activity.AidActivePercent = overall.AidActivePercent;
        range.Activity.PumpUsePercent = overall.PumpUsePercent;
        range.Activity.CgmActivePercent = overall.CgmActivePercent;
        return range;
    }

    /// <summary>The reads both tabs share, issued together.</summary>
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
        List<RateSegment> Scheduled,
        TherapyTimeline Timeline,
        long TickStart,
        bool HasTherapy);

    /// <summary>
    /// Basal delivered against the schedule (scaled by any override) and every bolus in the window,
    /// as <see cref="NetInsulinAboveSchedule"/> defines them.
    /// </summary>
    private async Task<Delivery> DeliveryAsync(Common common, Window window, IReadOnlyList<long> snapshotMills, CancellationToken ct)
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

        var (clipped, evidence) = NetInsulinAboveSchedule.ClipTempBasals(common.TempBasals, snapshotMills);
        var series = await basalSeriesBuilder.BuildAsync(
            clipped, tickStart, to, timeline.SnapshotAt(from).BasalRateAt(from), timeline, ct);
        var delivered = NetInsulinAboveSchedule.DeliveredFromSeries(series, to, evidence);

        var doses = common.Boluses
            .Where(b => b.Mills >= from && b.Mills < to)
            .Select(b => new InsulinDose(b.Mills, b.Insulin, b.Kind == BolusKind.Algorithm));

        return new Delivery(
            new NetInsulinAboveSchedule(delivered, scheduled, doses), evidence, scheduled, timeline, tickStart, hasTherapy);
    }

    private static LoopAnalysisMaskSources Sources(Common common, IReadOnlyList<long> snapshotMills, TimeZoneInfo zone, long to) => new()
    {
        Overrides = [.. SpansOf(common.Spans, StateSpanCategory.Override, to).Select(o => o.Interval)],
        TemporaryTargets = [.. TemporaryTargets(common.Spans, to).Select(s => s.Interval)],
        Exercise = [.. SpansOf(common.Spans, StateSpanCategory.Exercise, to).Select(s => s.Interval)],
        Illness = [.. SpansOf(common.Spans, StateSpanCategory.Illness, to).Select(s => s.Interval)],
        DataExclusions = [.. SpansOf(common.Spans, StateSpanCategory.DataExclusion, to).Select(s => s.Interval)],
        OpenLoop = OpenLoop(common.Spans, common.Devices, snapshotMills, zone, to).Intervals,
        Profiles = common.ProfileIntervals,
    };

    private static List<(StateSpan Span, MillsInterval Interval)> TemporaryTargets(
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
    /// Loop's maximum basal rate setting over time: the version of each active profile's settings
    /// in force, read once per profile, where it records one.
    /// </summary>
    private async Task<StepTimeline<double>> LoopMaximumBasalRateAsync(
        IReadOnlyList<ProfileSpan> profiles, long to, CancellationToken ct)
    {
        var versions = new Dictionary<string, TherapySettings[]>(StringComparer.Ordinal);
        foreach (var name in profiles.Select(p => p.ProfileName).Distinct(StringComparer.Ordinal))
            versions[name] = [.. (await therapySettingsRepository.GetByProfileNameAsync(name, ct))
                .OrderByDescending(v => v.Timestamp)];

        var steps = new List<Step<double>>();
        foreach (var profile in profiles)
        {
            var start = profile.StartMills;
            var end = profile.EndMills ?? to;
            var bounds = versions[profile.ProfileName]
                .Select(v => ToMills(DateTime.SpecifyKind(v.Timestamp, DateTimeKind.Utc)))
                .Where(m => m > start && m < end)
                .Append(start)
                .Append(end)
                .Distinct()
                .Order()
                .ToList();
            for (var i = 0; i + 1 < bounds.Count; i++)
            {
                var settings = ProfileVersionCache.ActiveAt(versions[profile.ProfileName], ToUtc(bounds[i]));
                if (settings?.LoopSettings?.MaximumBasalRatePerHour is { } maximum)
                    steps.Add(new Step<double>(bounds[i], bounds[i + 1], maximum));
            }
        }
        return StepTimeline<double>.LatestStartedWins(steps);
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
        public static Window Of(DateTime from, DateTime to) => new(ToMills(from), ToMills(to));

        public bool IsEmpty => To <= From;

        public bool Touches(Window other) => From <= other.To && other.From <= To;

        public Window Hull(Window other) => new(Math.Min(From, other.From), Math.Max(To, other.To));
    }
}
