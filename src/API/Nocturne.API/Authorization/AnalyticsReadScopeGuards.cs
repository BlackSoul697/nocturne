using Nocturne.API.Controllers.V4.Analytics;
using Nocturne.API.Services.Glucose;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Authorization;
using Nocturne.Core.Models.LoopAnalysis;
using Nocturne.Core.Models.Widget;

namespace Nocturne.API.Authorization;

/// <summary>
/// Per-category read scopes for the V4 analytics payloads that merge several data categories into
/// one response, the shape <see cref="ActogramReadScopeGuard"/> documents: the controller attribute
/// lists the categories as an OR so any caller holding one of them is admitted, and the guard
/// empties the categories the caller does not hold. Attribute arguments must be compile-time
/// constants, so each attribute repeats its admission list inline and the <c>AdmissionScopes</c>
/// members here are what the guard tests pin it against.
/// </summary>
internal static class AnalyticsReadScopes
{
    /// <summary>
    /// The two categories the retrospective, prediction and correlation payloads span. Glucose
    /// covers the sensor/meter/calibration/BG-check readings and everything derived from them;
    /// treatments covers insulin, carbs and the basal and IOB/COB series computed from them.
    /// </summary>
    public static readonly IReadOnlyList<string> GlucoseAndTreatments =
    [
        Scope.GlucoseRead,
        Scope.TreatmentsRead,
    ];

    public static bool Allows(IReadOnlySet<string> grantedScopes, string scope) =>
        Scope.Satisfies(grantedScopes, scope);
}

/// <summary>
/// Per-category read scopes for the dashboard chart, the widest merge in the API: glucose readings
/// and their threshold band, the treatment markers and the IOB/COB/basal series derived from them,
/// device and system events, profile switches, and the heart-rate and step overlays.
/// </summary>
/// <seealso cref="AnalyticsReadScopes"/>
internal static class ChartDataReadScopeGuard
{
    public static readonly IReadOnlyList<string> AdmissionScopes =
    [
        Scope.GlucoseRead,
        Scope.TreatmentsRead,
        Scope.DevicesRead,
        Scope.TherapyRead,
        Scope.HeartRateRead,
        Scope.StepCountRead,
        Scope.SleepRead,
    ];

    public static DashboardChartData Redact(
        DashboardChartData data,
        IReadOnlySet<string> grantedScopes)
    {
        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.GlucoseRead))
        {
            // Thresholds are the band edges of the glucose series, so they leave with it.
            data.GlucoseData = [];
            data.Thresholds = new ChartThresholdsDto();
            data.BgCheckMarkers = [];
        }

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead))
        {
            data.IobSeries = [];
            data.CobSeries = [];
            data.BasalSeries = [];
            data.BolusMarkers = [];
            data.CarbMarkers = [];
            data.BasalInjectionMarkers = [];
            data.OverrideSpans = [];
            data.TempBasalSpans = [];
            data.BasalDeliverySpans = [];
            data.DefaultBasalRate = 0;
            data.MaxBasalRate = 0;
            data.MaxIob = 0;
            data.MaxCob = 0;
        }

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.DevicesRead))
        {
            data.DeviceEventMarkers = [];
            data.SystemEventMarkers = [];
            data.PumpModeSpans = [];
        }

        // Read off both the basal delivery spans and the pump status, so it needs both scopes.
        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead)
            || !AnalyticsReadScopes.Allows(grantedScopes, Scope.DevicesRead))
            data.LastPumpSyncMills = null;

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.TherapyRead))
            data.ProfileSpans = [];

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.HeartRateRead))
            data.HeartRateSeries = [];

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.StepCountRead))
            data.StepSeries = [];

        // ActivitySpans is the one field with two governing categories: the exercise, illness and
        // travel state spans are treatment annotations, while the sleep sessions projected in
        // beside them are their own category, so it is filtered per span rather than emptied.
        data.ActivitySpans = [.. data.ActivitySpans.Where(s => AnalyticsReadScopes.Allows(
            grantedScopes,
            s.Kind == ChartSpanKind.Sleep ? Scope.SleepRead : Scope.TreatmentsRead))];

        return data;
    }
}

/// <summary>
/// Per-category read scopes for the retrospective "what was happening at this moment" payloads:
/// the glucose value and direction on one side, the IOB/COB/basal state and the recent treatments
/// that produced it on the other.
/// </summary>
/// <seealso cref="AnalyticsReadScopes"/>
internal static class RetrospectiveReadScopeGuard
{
    public static readonly IReadOnlyList<string> AdmissionScopes = AnalyticsReadScopes.GlucoseAndTreatments;

    public static RetrospectiveDataResponse Redact(
        RetrospectiveDataResponse data,
        IReadOnlySet<string> grantedScopes)
    {
        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.GlucoseRead))
            data.Glucose = null;

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead))
        {
            data.Iob = null;
            data.Cob = null;
            data.Basal = null;
            data.RecentTreatments = [];
        }

        return data;
    }

    public static RetrospectiveTimelineResponse Redact(
        RetrospectiveTimelineResponse data,
        IReadOnlySet<string> grantedScopes)
    {
        var glucose = AnalyticsReadScopes.Allows(grantedScopes, Scope.GlucoseRead);
        var treatments = AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead);

        if (glucose && treatments)
            return data;

        // The timeline interleaves both categories in every point, so redaction is per field
        // rather than per collection; the frontend already renders a point with a null glucose.
        foreach (var point in data.Data ?? [])
        {
            if (!glucose)
            {
                point.Glucose = null;
                point.GlucoseDirection = null;
            }

            if (!treatments)
            {
                point.Iob = 0;
                point.BolusIob = 0;
                point.BasalIob = 0;
                point.Cob = 0;
                point.BasalRate = 0;
                point.IsTemp = false;
            }
        }

        return data;
    }
}

/// <summary>
/// Per-category read scopes for the glucose forecast: the curves and the current/eventual glucose
/// they run from are the glucose category, while the insulin and carb state the forecast was
/// computed against is the treatment category.
/// </summary>
/// <seealso cref="AnalyticsReadScopes"/>
internal static class PredictionReadScopeGuard
{
    public static readonly IReadOnlyList<string> AdmissionScopes = AnalyticsReadScopes.GlucoseAndTreatments;

    public static GlucosePredictionResponse Redact(
        GlucosePredictionResponse data,
        IReadOnlySet<string> grantedScopes)
    {
        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.GlucoseRead))
        {
            data.CurrentBg = 0;
            data.Delta = 0;
            data.EventualBg = 0;
            data.Predictions = new PredictionCurves();
        }

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead))
        {
            data.Iob = 0;
            data.Cob = 0;
            data.SensitivityRatio = null;
        }

        return data;
    }
}

/// <summary>
/// Per-category read scopes for the correlation lookup, which fans one correlation id out across
/// the glucose repositories (sensor, meter, calibration, BG check) and the treatment repositories
/// (bolus, carb intake, note, bolus calculation).
/// </summary>
/// <remarks>
/// Redaction here is a decision not to query rather than a decision to empty: the controller skips
/// the repositories whose category the caller does not hold, so a partial read costs less than a
/// full one.
/// </remarks>
/// <seealso cref="AnalyticsReadScopes"/>
internal static class CorrelationReadScopeGuard
{
    public static readonly IReadOnlyList<string> AdmissionScopes = AnalyticsReadScopes.GlucoseAndTreatments;

    public static bool AllowsGlucose(IReadOnlySet<string> grantedScopes) =>
        AnalyticsReadScopes.Allows(grantedScopes, Scope.GlucoseRead);

    public static bool AllowsTreatments(IReadOnlySet<string> grantedScopes) =>
        AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead);
}

/// <summary>
/// Per-category read scopes for the widget summary, which packs the current and historical glucose,
/// the IOB/COB state, the tracker statuses and the firing alarm into one small payload.
/// </summary>
/// <remarks>
/// Trackers carry no <see cref="ShareDataCategories"/> category and are gated by the fallback
/// policy wherever else they are served, so they are not redacted here.
/// </remarks>
/// <seealso cref="AnalyticsReadScopes"/>
internal static class WidgetSummaryReadScopeGuard
{
    public static readonly IReadOnlyList<string> AdmissionScopes =
    [
        Scope.GlucoseRead,
        Scope.TreatmentsRead,
        Scope.AlertsRead,
    ];

    public static V4SummaryResponse Redact(
        V4SummaryResponse data,
        IReadOnlySet<string> grantedScopes)
    {
        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.GlucoseRead))
        {
            data.Current = null;
            data.History = [];
            data.Predictions = null;
        }

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead))
        {
            data.Iob = 0;
            data.Cob = 0;
        }

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.AlertsRead))
            data.Alarm = null;

        return data;
    }
}

/// <summary>
/// Per-category read scopes for the "right now" therapy state: the pump mode and the pump's
/// reservoir and battery readings are the device category, the sensitivity adjustment is read off the
/// active therapy profile, and the thresholds follow glucose as in <see cref="ChartDataReadScopeGuard"/>.
/// Glucose alone admits the caller because the dashboard sparkline draws its band from these
/// thresholds for any viewer of the glucose series.
/// </summary>
/// <seealso cref="AnalyticsReadScopes"/>
internal static class CurrentTherapyStateReadScopeGuard
{
    public static readonly IReadOnlyList<string> AdmissionScopes =
    [
        Scope.GlucoseRead,
        Scope.DevicesRead,
        Scope.TherapyRead,
    ];

    public static CurrentTherapyStateResponse Redact(
        CurrentTherapyStateResponse data,
        IReadOnlySet<string> grantedScopes)
    {
        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.DevicesRead))
        {
            data.CurrentPumpMode = null;
            data.Reservoir = null;
            data.PumpBatteryPercent = null;
            data.PumpBatteryVoltage = null;
        }

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.GlucoseRead))
            data.Thresholds = new ChartThresholdsDto();

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.TherapyRead))
            data.SensitivityPercent = null;

        return data;
    }
}

/// <summary>
/// Per-category read scopes for the statistics admitted on <c>glucose.read</c> alone that also
/// summarise insulin and carbs, and count sensor sessions from device events. Category row level
/// security hides those categories only from shares, so for a member, guest, OAuth client or token
/// the narrowing has to happen here.
/// </summary>
/// <remarks>
/// <c>GET /periods</c> is served from <see cref="Nocturne.API.Services.Caching.ITenantCache"/>,
/// whose value holds every category; <see cref="Redact"/> narrows a copy and never the cached
/// instance. The treatment and device narrowings are independent.
/// </remarks>
/// <seealso cref="AnalyticsReadScopes"/>
internal static class StatisticsReadScopeGuard
{
    public static bool AllowsTreatments(IReadOnlySet<string> grantedScopes) =>
        AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead);

    public static bool AllowsDevices(IReadOnlySet<string> grantedScopes) =>
        AnalyticsReadScopes.Allows(grantedScopes, Scope.DevicesRead);

    public static MultiPeriodStatistics Redact(
        MultiPeriodStatistics data,
        IReadOnlySet<string> grantedScopes)
    {
        var treatments = AllowsTreatments(grantedScopes);
        var devices = AllowsDevices(grantedScopes);

        if (treatments && devices)
            return data;

        return new MultiPeriodStatistics
        {
            LastDay = Narrow(data.LastDay, treatments, devices),
            Last3Days = Narrow(data.Last3Days, treatments, devices),
            LastWeek = Narrow(data.LastWeek, treatments, devices),
            LastMonth = Narrow(data.LastMonth, treatments, devices),
            Last90Days = Narrow(data.Last90Days, treatments, devices),
            LastUpdated = data.LastUpdated,
        };
    }

    private static PeriodStatistics Narrow(PeriodStatistics period, bool treatments, bool devices) => new()
    {
        PeriodDays = period.PeriodDays,
        StartDate = period.StartDate,
        EndDate = period.EndDate,
        Analytics = devices ? period.Analytics : WithoutSensorWarmups(period.Analytics),
        TreatmentSummary = treatments ? period.TreatmentSummary : null,
        InsulinDelivery = treatments ? period.InsulinDelivery : null,
        HasSufficientData = period.HasSufficientData,
        Gmi = period.Gmi,
        Reliability = period.Reliability,
        EntryCount = period.EntryCount,
        TreatmentCount = treatments ? period.TreatmentCount : 0,
    };

    private static GlucoseAnalytics? WithoutSensorWarmups(GlucoseAnalytics? analytics) =>
        analytics is null
            ? null
            : new GlucoseAnalytics
            {
                BasicStats = analytics.BasicStats,
                TimeInRange = analytics.TimeInRange,
                GlycemicVariability = analytics.GlycemicVariability,
                DataQuality = new DataQuality
                {
                    TotalReadings = analytics.DataQuality.TotalReadings,
                    MissingReadings = analytics.DataQuality.MissingReadings,
                    DataCompleteness = analytics.DataQuality.DataCompleteness,
                    CgmActivePercent = analytics.DataQuality.CgmActivePercent,
                    GapAnalysis = analytics.DataQuality.GapAnalysis,
                    NoiseLevel = analytics.DataQuality.NoiseLevel,
                    CalibrationEvents = analytics.DataQuality.CalibrationEvents,
                    SensorWarmups = 0,
                },
                Reliability = analytics.Reliability,
                Time = analytics.Time,
                ClinicalAssessment = analytics.ClinicalAssessment,
            };
}

/// <summary>
/// Per-category read scopes for Loop Analysis, admitted on <c>reports.read</c>: glucose is the
/// glucose category; delivered basal, the insulin above schedule, IOB/COB, boluses and carbs are
/// treatments; the profile names and the separate schedule series (scheduled basal, target range)
/// are therapy settings. Both of those series also carry treatments (the scheduled basal is scaled
/// by any override, the target range overlays temporary targets), so they need treatments too.
/// The masks follow the categories of the spans they are drawn from, as
/// <see cref="ChartDataReadScopeGuard"/> serves them: overrides, temporary targets, exercise and
/// illness are treatments, pump mode and the registered pumps are devices.
/// </summary>
/// <remarks>
/// <para>
/// The schedule can be worked back from the delivered basal and the components of insulin above
/// schedule. That is not a disclosure past <c>treatments.read</c>: a schedule value carried inside
/// delivery is treatment data, as <see cref="ChartDataReadScopeGuard"/> serves the basal series and
/// its scheduled rate under that scope. <c>therapy.read</c> gates the schedule and target series
/// served in their own right, and the profile names.
/// </para>
/// <para>The response is built per request and never cached server-side, so it is narrowed in place.</para>
/// </remarks>
/// <seealso cref="AnalyticsReadScopes"/>
internal static class LoopAnalysisReadScopeGuard
{
    public const string AdmissionScope = Scope.ReportsRead;

    /// <summary>
    /// Drops from <paramref name="masks"/> every part the caller could not otherwise see: a profile
    /// name without <c>therapy.read</c>, and each mask drawn from a category the caller lacks, which
    /// would otherwise show where those spans fell through the analysed time.
    /// </summary>
    /// <returns>The query parameter names that were ignored, for the response to report.</returns>
    public static List<string> NarrowFilter(LoopAnalysisMasks masks, IReadOnlySet<string> grantedScopes)
    {
        var ignored = new List<string>();
        void Ignore(bool set, string name, Action clear)
        {
            if (!set)
                return;
            clear();
            ignored.Add(name);
        }

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.TherapyRead))
            Ignore(masks.ProfileName is not null, "profileName", () => masks.ProfileName = null);

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead))
        {
            Ignore(masks.ExcludeOverrides, "excludeOverrides", () => masks.ExcludeOverrides = false);
            Ignore(masks.ExcludeTemporaryTargets, "excludeTemporaryTargets", () => masks.ExcludeTemporaryTargets = false);
            Ignore(masks.ExcludeExercise, "excludeExercise", () => masks.ExcludeExercise = false);
            Ignore(masks.ExcludeIllness, "excludeIllness", () => masks.ExcludeIllness = false);
        }

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.DevicesRead))
            Ignore(masks.ExcludeOpenLoop, "excludeOpenLoop", () => masks.ExcludeOpenLoop = false);

        return ignored;
    }

    /// <summary>
    /// The series the caller's scopes withhold. <see cref="Redact"/> empties exactly these, so the
    /// response's <see cref="LoopAnalysisResponse.WithheldSeries"/> and its nulls cannot disagree.
    /// </summary>
    public static HashSet<LoopAnalysisSeries> Withheld(IReadOnlySet<string> grantedScopes)
    {
        var glucose = AnalyticsReadScopes.Allows(grantedScopes, Scope.GlucoseRead);
        var treatments = AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead);
        var therapy = AnalyticsReadScopes.Allows(grantedScopes, Scope.TherapyRead);

        var withheld = new HashSet<LoopAnalysisSeries>();
        void Withhold(bool hidden, params LoopAnalysisSeries[] series)
        {
            if (hidden)
                withheld.UnionWith(series);
        }

        Withhold(!glucose, LoopAnalysisSeries.Glucose);
        Withhold(!treatments,
            LoopAnalysisSeries.DeliveredBasal,
            LoopAnalysisSeries.NetLoopCorrection,
            LoopAnalysisSeries.NetLoopCorrectionComponents,
            LoopAnalysisSeries.HourlyNetLoopCorrection,
            LoopAnalysisSeries.Iob,
            LoopAnalysisSeries.Cob,
            LoopAnalysisSeries.ManualBoluses,
            LoopAnalysisSeries.Carbs,
            LoopAnalysisSeries.InsulinSummary,
            LoopAnalysisSeries.NetLoopCorrectionSummary);
        Withhold(!therapy || !treatments,
            LoopAnalysisSeries.ScheduledBasal,
            LoopAnalysisSeries.Target,
            LoopAnalysisSeries.DeliveredToScheduledRatio);
        Withhold(!therapy, LoopAnalysisSeries.ProfileNames);
        return withheld;
    }

    public static DailyProfileResponse Redact(DailyProfileResponse data, IReadOnlySet<string> grantedScopes)
    {
        var withheld = Withheld(grantedScopes);
        data.WithheldSeries = [.. withheld.Order()];
        if (withheld.Count == 0)
            return data;

        Narrow(data.Primary, withheld);
        if (data.Comparison is { } comparison)
            Narrow(comparison, withheld);
        return data;
    }

    /// <summary>
    /// The prediction figures the caller's scopes withhold, as <see cref="Withheld"/> is for the
    /// Daily profile. A curve's accuracy compares the APS snapshots, which are devices, with the
    /// sensor readings, which are glucose, so it needs both. Its context is drawn from treatments.
    /// </summary>
    public static HashSet<LoopAnalysisSeries> WithheldPredictions(IReadOnlySet<string> grantedScopes)
    {
        var glucose = AnalyticsReadScopes.Allows(grantedScopes, Scope.GlucoseRead);
        var devices = AnalyticsReadScopes.Allows(grantedScopes, Scope.DevicesRead);
        var treatments = AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead);

        var withheld = new HashSet<LoopAnalysisSeries>();
        if (!devices)
            withheld.Add(LoopAnalysisSeries.PredictionCount);
        if (!glucose || !devices)
            withheld.Add(LoopAnalysisSeries.PredictionCurves);
        if (!treatments)
            withheld.Add(LoopAnalysisSeries.PredictionContexts);
        return withheld;
    }

    public static PredictionAccuracyResponse Redact(PredictionAccuracyResponse data, IReadOnlySet<string> grantedScopes)
    {
        var withheld = WithheldPredictions(grantedScopes);
        data.WithheldSeries = [.. withheld.Order()];
        if (withheld.Count == 0)
            return data;

        Narrow(data.Primary, withheld);
        if (data.Comparison is { } comparison)
            Narrow(comparison, withheld);
        return data;
    }

    private static void Narrow(PredictionAccuracyRange range, HashSet<LoopAnalysisSeries> withheld)
    {
        if (withheld.Contains(LoopAnalysisSeries.PredictionCount))
        {
            range.Predictions = 0;
            range.Algorithms = [];
        }

        if (withheld.Contains(LoopAnalysisSeries.PredictionCurves))
            range.Curves = [];

        if (withheld.Contains(LoopAnalysisSeries.PredictionContexts))
        {
            var everyContext = Enum.GetValues<PredictionContext>().Length;
            range.Curves.RemoveAll(c => c.Contexts.Count < everyContext);
            foreach (var curve in range.Curves)
                curve.ByContext = [];
        }
    }

    private static void Narrow(DailyProfileRange range, HashSet<LoopAnalysisSeries> withheld)
    {
        bool Hidden(LoopAnalysisSeries series) => withheld.Contains(series);

        foreach (var slot in range.Slots)
        {
            if (Hidden(LoopAnalysisSeries.Glucose)) slot.Glucose = null;
            if (Hidden(LoopAnalysisSeries.ScheduledBasal)) slot.ScheduledBasal = null;
            if (Hidden(LoopAnalysisSeries.DeliveredBasal)) slot.DeliveredBasal = null;
            if (Hidden(LoopAnalysisSeries.NetLoopCorrection)) slot.NetLoopCorrection = null;
            if (Hidden(LoopAnalysisSeries.NetLoopCorrectionComponents))
            {
                slot.ManualTemp = null;
                slot.Suspend = null;
                slot.Inferred = null;
            }
            if (Hidden(LoopAnalysisSeries.Iob))
            {
                slot.Iob = null;
                slot.IobSource = null;
            }
            if (Hidden(LoopAnalysisSeries.Cob))
            {
                slot.Cob = null;
                slot.CobSource = null;
            }
            if (Hidden(LoopAnalysisSeries.ManualBoluses)) slot.ManualBoluses = null;
            if (Hidden(LoopAnalysisSeries.Carbs)) slot.Carbs = null;
            if (Hidden(LoopAnalysisSeries.Target))
            {
                slot.TargetLow = null;
                slot.TargetHigh = null;
            }
        }

        foreach (var day in range.Days)
        {
            if (Hidden(LoopAnalysisSeries.Glucose)) day.Coverage.Glucose = null;
            if (Hidden(LoopAnalysisSeries.ScheduledBasal)) day.Coverage.ScheduledBasal = null;
            if (Hidden(LoopAnalysisSeries.DeliveredBasal)) day.Coverage.DeliveredBasal = null;
            if (Hidden(LoopAnalysisSeries.NetLoopCorrection))
            {
                day.Coverage.NetLoopCorrection = null;
                day.NetLoopCorrection = null;
            }
            if (Hidden(LoopAnalysisSeries.Iob) || Hidden(LoopAnalysisSeries.Cob)) day.Coverage.IobCob = null;
            if (Hidden(LoopAnalysisSeries.InsulinSummary))
            {
                day.TotalInsulin = null;
                day.DeliveredBasal = null;
                day.AutomaticBolus = null;
                day.ManualBolus = null;
            }
        }

        if (Hidden(LoopAnalysisSeries.HourlyNetLoopCorrection)) range.HourlyNetLoopCorrection = [];
        if (Hidden(LoopAnalysisSeries.NetLoopCorrectionSummary))
        {
            range.Summary.NetLoopCorrectionPerDay = null;
            range.Summary.NetLoopCorrectionTddPercent = null;
            range.Summary.NetLoopCorrectionDays = 0;
        }
        if (Hidden(LoopAnalysisSeries.InsulinSummary)) range.Summary.Insulin = null;
        if (Hidden(LoopAnalysisSeries.DeliveredToScheduledRatio)) range.Summary.DeliveredToScheduledRatio = null;
        if (Hidden(LoopAnalysisSeries.ProfileNames)) range.ProfileNames = [];

        range.Summary.DaysWithData = range.Days.Count(day =>
            (day.HasGlucose && !Hidden(LoopAnalysisSeries.Glucose))
            || (day.HasTreatments && !Hidden(LoopAnalysisSeries.DeliveredBasal)));
    }
}
