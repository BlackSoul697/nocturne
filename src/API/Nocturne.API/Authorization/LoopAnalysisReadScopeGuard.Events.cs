using Nocturne.Core.Models.Authorization;
using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.API.Authorization;

/// <summary>
/// The Meals &amp; events tab. An anchor is drawn from a category as a series is: meals, correction
/// boluses and the starts of exercise, overrides and temporary targets are treatments, sensor
/// starts are devices, and an unannounced rise is read off glucose and the carbs and automatic
/// boluses around it, so it needs both. A caller may ask only for anchors it could read, since the
/// curves and counts aligned on an anchor would otherwise place each one in time. The isolation and
/// corroboration options read meals and boluses, so they need treatments too.
/// </summary>
internal static partial class LoopAnalysisReadScopeGuard
{
    private static readonly IReadOnlyDictionary<EventAnchorKind, string[]> AnchorScopes =
        new Dictionary<EventAnchorKind, string[]>
        {
            [EventAnchorKind.Meal] = [Scope.TreatmentsRead],
            [EventAnchorKind.CorrectionBolus] = [Scope.TreatmentsRead],
            [EventAnchorKind.SensorStart] = [Scope.DevicesRead],
            [EventAnchorKind.ExerciseStart] = [Scope.TreatmentsRead],
            [EventAnchorKind.OverrideStart] = [Scope.TreatmentsRead],
            [EventAnchorKind.TemporaryTargetStart] = [Scope.TreatmentsRead],
            [EventAnchorKind.UnannouncedRise] = [Scope.GlucoseRead, Scope.TreatmentsRead],
        };

    /// <summary>The options beside the cohorts, by query parameter, and how to drop each.</summary>
    private static readonly (string Name, Func<EventResponseDefinition, bool> IsSet, Action<EventResponseDefinition> Clear)[] EventOptions =
    [
        ("isolatedOnly", d => d.IsolatedOnly, d => d.IsolatedOnly = false),
        ("requireBolusCluster", d => d.RequireBolusCluster, d => d.RequireBolusCluster = false),
    ];

    /// <summary>The first scope an anchor kind needs that the caller lacks, or null.</summary>
    private static string? MissingScope(EventAnchorKind anchor, IReadOnlySet<string> grantedScopes) =>
        AnchorScopes[anchor].FirstOrDefault(scope => !AnalyticsReadScopes.Allows(grantedScopes, scope));

    /// <summary>
    /// Drops from <paramref name="definition"/> every cohort whose anchor the caller could not read,
    /// and the options without <c>treatments.read</c>.
    /// </summary>
    /// <returns>The query parameters ignored: each dropped cohort's <c>anchor{n}</c>, and the options.</returns>
    public static List<string> NarrowEvents(EventResponseDefinition definition, IReadOnlySet<string> grantedScopes)
    {
        var ignored = definition.Cohorts
            .Where(c => MissingScope(c.Anchor, grantedScopes) is not null)
            .Select(c => $"anchor{c.Cohort}")
            .ToList();
        definition.Cohorts.RemoveAll(c => MissingScope(c.Anchor, grantedScopes) is not null);

        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead))
        {
            foreach (var option in EventOptions.Where(o => o.IsSet(definition)))
            {
                option.Clear(definition);
                ignored.Add(option.Name);
            }
        }
        return ignored;
    }

    public static List<EventUnavailableAnchor> UnavailableAnchors(IReadOnlySet<string> grantedScopes) =>
    [
        .. AnchorScopes.Keys
            .Select(anchor => (anchor, scope: MissingScope(anchor, grantedScopes)))
            .Where(a => a.scope is not null)
            .Select(a => new EventUnavailableAnchor { Anchor = a.anchor, RequiredScope = a.scope! }),
    ];

    /// <summary>The event options <see cref="NarrowEvents"/> would drop for this caller, set or not.</summary>
    public static List<LoopAnalysisUnavailableFilter> UnavailableEventFilters(IReadOnlySet<string> grantedScopes) =>
        AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead)
            ? []
            : [.. EventOptions.Select(o => new LoopAnalysisUnavailableFilter { Filter = o.Name, RequiredScope = Scope.TreatmentsRead })];

    /// <summary>The event series and figures the caller's scopes withhold, as <see cref="Withheld"/> is for the Daily profile.</summary>
    public static HashSet<LoopAnalysisSeries> WithheldEvents(IReadOnlySet<string> grantedScopes)
    {
        var withheld = new HashSet<LoopAnalysisSeries>();
        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.GlucoseRead))
            withheld.Add(LoopAnalysisSeries.EventGlucose);
        if (!AnalyticsReadScopes.Allows(grantedScopes, Scope.TreatmentsRead))
            withheld.UnionWith([
                LoopAnalysisSeries.EventNetInsulin, LoopAnalysisSeries.EventIob,
                LoopAnalysisSeries.EventCob, LoopAnalysisSeries.EventList,
            ]);
        return withheld;
    }

    /// <summary>
    /// Narrows the response to the caller. The definition was narrowed before it was computed; any
    /// cohort it still holds that the caller could not have asked for is dropped here as well, so
    /// the response never depends on that having happened.
    /// </summary>
    public static EventResponseReport Redact(EventResponseReport data, IReadOnlySet<string> grantedScopes)
    {
        NarrowEvents(data.Definition, grantedScopes);
        var kept = data.Definition.Cohorts.Select(c => c.Cohort).ToHashSet();
        var withheld = WithheldEvents(grantedScopes);
        data.WithheldSeries = [.. withheld.Order()];

        foreach (var range in new[] { data.Primary, data.Comparison }.OfType<EventResponseRange>())
        {
            range.Cohorts.RemoveAll(c => !kept.Contains(c.Cohort));
            Narrow(range, withheld);
        }
        return data;
    }

    private static void Narrow(EventResponseRange range, HashSet<LoopAnalysisSeries> withheld)
    {
        if (withheld.Count == 0)
            return;
        bool Hidden(LoopAnalysisSeries series) => withheld.Contains(series);

        if (Hidden(LoopAnalysisSeries.EventList))
            range.Events = [];
        foreach (var e in range.Events)
            ClearMetrics(e.Metrics, Hidden);

        foreach (var cohort in range.Cohorts)
        {
            foreach (var slot in cohort.Slots)
            {
                if (Hidden(LoopAnalysisSeries.EventGlucose))
                {
                    slot.Glucose = null;
                    slot.GlucoseChange = null;
                }
                if (Hidden(LoopAnalysisSeries.EventNetInsulin))
                {
                    slot.NetLoopCorrection = null;
                    slot.NetInsulinSinceAnchor = null;
                    slot.NetLoopCorrectionSinceAnchor = null;
                }
                if (Hidden(LoopAnalysisSeries.EventIob)) slot.Iob = null;
                if (Hidden(LoopAnalysisSeries.EventCob)) slot.Cob = null;
            }

            ClearMetrics(cohort.Metrics, Hidden);
        }
    }

    /// <summary>
    /// Each figure of <see cref="EventMetrics"/> by the series it belongs to; the same names on
    /// <see cref="EventMetricsSummary"/> carry its spread.
    /// </summary>
    private static readonly (LoopAnalysisSeries Series, string[] Metrics)[] MetricSeries =
    [
        (LoopAnalysisSeries.EventGlucose,
        [
            nameof(EventMetrics.StartGlucose), nameof(EventMetrics.PeakRise), nameof(EventMetrics.MinutesToPeak),
            nameof(EventMetrics.GlucoseAt2Hours), nameof(EventMetrics.GlucoseAt4Hours), nameof(EventMetrics.MinutesBackToStart),
        ]),
        (LoopAnalysisSeries.EventNetInsulin,
        [
            nameof(EventMetrics.TotalInsulin), nameof(EventMetrics.NetInsulin), nameof(EventMetrics.GramsPerUnit),
            nameof(EventMetrics.NetInsulinFirst30), nameof(EventMetrics.NetInsulinFirst60), nameof(EventMetrics.NetInsulinFirst120),
        ]),
        (LoopAnalysisSeries.EventIob, [nameof(EventMetrics.IobAtAnchor), nameof(EventMetrics.IobAtEnd)]),
    ];

    /// <param name="metrics">An <see cref="EventMetrics"/> or an <see cref="EventMetricsSummary"/>.</param>
    /// <param name="hidden">Whether a series is withheld.</param>
    private static void ClearMetrics(object metrics, Func<LoopAnalysisSeries, bool> hidden)
    {
        foreach (var (_, names) in MetricSeries.Where(m => hidden(m.Series)))
            foreach (var name in names)
                metrics.GetType().GetProperty(name)!.SetValue(metrics, null);
    }
}
