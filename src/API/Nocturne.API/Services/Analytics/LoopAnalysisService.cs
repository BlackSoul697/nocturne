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
/// <see cref="ChartDataService.BufferMs"/> before the window so IOB and COB at its start see the
/// doses still acting.
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
    ICobCalculator cobCalculator,
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
        var primary = Window.Of(filter.From, filter.To);
        var comparison = filter.CompareFrom is { } compareFrom && filter.CompareTo is { } compareTo
            ? Window.Of(compareFrom, compareTo)
            : (Window?)null;

        var (zone, reason) = await tenantClock.ResolveAsync(ct);
        var response = new DailyProfileResponse { TimeZoneUnavailableReason = reason };
        if (zone is null)
        {
            response.Primary = primary.Empty();
            response.Comparison = comparison?.Empty();
            return response;
        }

        if (comparison is not { } other)
        {
            response.Primary = Calculate(await LoadAsync(primary, zone, ct), filter, primary);
            return response;
        }

        if (primary.Touches(other))
        {
            var both = await LoadAsync(primary.Hull(other), zone, ct);
            response.Primary = Calculate(both, filter, primary);
            response.Comparison = Calculate(both, filter, other);
        }
        else
        {
            response.Primary = Calculate(await LoadAsync(primary, zone, ct), filter, primary);
            response.Comparison = Calculate(await LoadAsync(other, zone, ct), filter, other);
        }
        return response;
    }

    private static DailyProfileRange Calculate(Loaded loaded, LoopAnalysisFilter filter, Window window) =>
        DailyProfileCalculator.Calculate(
            loaded.Inputs,
            LoopAnalysisMask.Build(filter, window.From, window.To, loaded.Inputs.Zone, loaded.Sources),
            window.From,
            window.To);

    private sealed record Loaded(DailyProfileInputs Inputs, LoopAnalysisMaskSources Sources);

    private async Task<Loaded> LoadAsync(Window window, TimeZoneInfo zone, CancellationToken ct)
    {
        var from = window.From;
        var to = window.To;
        var fromUtc = ToUtc(from);
        var toUtc = ToUtc(to);
        var lookbackUtc = ToUtc(from - ChartDataService.BufferMs);

        var glucoseTask = glucoseRepository.GetForAnalyticsAsync(fromUtc, toUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var bolusTask = bolusRepository.GetAsync(lookbackUtc, toUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var carbTask = carbIntakeRepository.GetAsync(lookbackUtc, toUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var tempBasalTask = tempBasalRepository.GetAsync(lookbackUtc, toUtc, null, null, int.MaxValue, descending: false, ct: ct);
        var apsTask = apsSnapshotRepository.GetIobCobPointsAsync(lookbackUtc, toUtc, ct);
        var spanTask = stateSpanRepository.GetByCategories(MaskCategories, fromUtc, toUtc, ct);
        var deviceTask = patientDeviceRepository.GetByDateRangeAsync(fromUtc, toUtc, ct);
        await Task.WhenAll(glucoseTask, bolusTask, carbTask, tempBasalTask, apsTask, spanTask, deviceTask);

        var glucose = await canonicalGlucose.SelectAsync([.. await glucoseTask], ct);
        var boluses = (await bolusTask).ToList();
        var carbs = (await carbTask).ToList();
        var tempBasals = (await tempBasalTask).ToList();
        var spans = await spanTask;

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

        var (clipped, recorded, unknown) = NetInsulinAboveSchedule.ClipTempBasals(tempBasals);
        var series = await basalSeriesBuilder.BuildAsync(
            clipped, tickStart, to, timeline.SnapshotAt(from).BasalRateAt(from), timeline, ct);
        var delivered = NetInsulinAboveSchedule.DeliveredFromSeries(series, to, recorded, unknown);

        var doses = boluses
            .Where(b => b.Mills >= from && b.Mills < to)
            .Select(b => new InsulinDose(b.Mills, b.Insulin, b.Kind == BolusKind.Algorithm));

        var iobCob = IobCobSeries.Build(
            iobCalculator, cobCalculator, boluses, carbs, tempBasals, timeline, await apsTask,
            tickStart, to - 1, TickMs, ct);

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
            TargetAt = hasTherapy ? await TargetRangesAsync(profiles, zone, ct) : null,
            Profiles = profileIntervals,
        };

        var sources = new LoopAnalysisMaskSources
        {
            Overrides = [.. overrides.Select(o => o.Interval)],
            TemporaryTargets = [.. SpansOf(spans, StateSpanCategory.TemporaryTarget, to)
                .Where(s => s.Span.State != nameof(TemporaryTargetState.Cancelled))
                .Select(s => s.Interval)],
            Exercise = [.. SpansOf(spans, StateSpanCategory.Exercise, to).Select(s => s.Interval)],
            Illness = [.. SpansOf(spans, StateSpanCategory.Illness, to).Select(s => s.Interval)],
            DataExclusions = [.. SpansOf(spans, StateSpanCategory.DataExclusion, to).Select(s => s.Interval)],
            OpenLoop = [.. OpenLoop(spans, await deviceTask, to)],
            Profiles = profileIntervals,
        };

        return new Loaded(inputs, sources);
    }

    private async Task<List<BasalSegment>> ScheduleAsync(long from, long to, CancellationToken ct)
    {
        var segments = new List<BasalSegment>();
        await foreach (var segment in basalSegmentService.GetSegmentsAsync(from, to, ct))
            segments.Add(segment);
        return segments;
    }

    /// <summary>
    /// The scheduled target range at an instant: the activation's own targets when it carries them,
    /// else the profile's stored version in force, as <see cref="ActiveSchedule"/> reads it for one
    /// instant, from schedules read once per profile.
    /// </summary>
    private async Task<Func<long, (double Low, double High)?>> TargetRangesAsync(
        IReadOnlyList<ProfileSpan> profiles, TimeZoneInfo zone, CancellationToken ct)
    {
        var versions = new Dictionary<string, TargetRangeSchedule[]>(StringComparer.Ordinal);
        foreach (var name in profiles.Select(p => p.ProfileName).Distinct(StringComparer.Ordinal))
            versions[name] = [.. (await targetRangeScheduleRepository.GetByProfileNameAsync(name, ct))
                .OrderByDescending(v => v.Timestamp)];

        var ordered = profiles.OrderBy(p => p.StartMills).ToArray();
        return mills =>
        {
            var span = ordered.LastOrDefault(p => p.StartMills <= mills && (p.EndMills ?? long.MaxValue) > mills);
            if (span is null)
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

    private static IEnumerable<MillsInterval> OpenLoop(
        Dictionary<StateSpanCategory, List<StateSpan>> spans, IEnumerable<PatientDevice> devices, long to)
    {
        foreach (var mode in SpansOf(spans, StateSpanCategory.PumpMode, to)
                     .Where(s => s.Span.State is nameof(PumpModeState.Manual) or nameof(PumpModeState.Off)))
            yield return mode.Interval;

        foreach (var pump in devices.Where(d =>
                     d.DeviceCategory == DeviceCategory.InsulinPump && d.AidAlgorithm == AidAlgorithm.None))
        {
            yield return new MillsInterval(
                pump.StartDate is { } start ? ToMills(start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)) : long.MinValue,
                pump.EndDate is { } end ? ToMills(end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)) : to);
        }
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

        public bool Touches(Window other) => From <= other.To && other.From <= To;

        public Window Hull(Window other) => new(Math.Min(From, other.From), Math.Max(To, other.To));

        public DailyProfileRange Empty() => new()
        {
            From = ToUtc(From),
            To = ToUtc(To),
            SlotMinutes = DailyProfileCalculator.SlotMinutes,
        };
    }
}
