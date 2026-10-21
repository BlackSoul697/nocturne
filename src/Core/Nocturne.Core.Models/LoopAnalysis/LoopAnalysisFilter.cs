using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.LoopAnalysis;

/// <summary>
/// What is analysed within a range. Each exclusion masks the matching time, not the whole day it
/// falls on; <see cref="StateSpanCategory.DataExclusion"/> spans are always taken out of glucose
/// whatever is set here.
/// </summary>
public class LoopAnalysisMasks
{
    /// <summary>Local weekdays analysed.</summary>
    public List<DayOfWeek> Days { get; set; } = [.. Enum.GetValues<DayOfWeek>()];

    public bool ExcludeOverrides { get; set; }

    public bool ExcludeTemporaryTargets { get; set; }

    public bool ExcludeExercise { get; set; }

    public bool ExcludeIllness { get; set; } = true;

    /// <summary>Masks time the pump was in manual mode or had no AID algorithm.</summary>
    public bool ExcludeOpenLoop { get; set; } = true;

    /// <summary>Limits the analysis to time this profile was active.</summary>
    public string? ProfileName { get; set; }
}

/// <summary>The resolved ranges and masks every Loop Analysis calculation takes.</summary>
public class LoopAnalysisFilter
{
    /// <summary>Start of the range (inclusive, UTC).</summary>
    public DateTime From { get; set; }

    /// <summary>End of the range (exclusive, UTC); never before <see cref="From"/>.</summary>
    public DateTime To { get; set; }

    /// <summary>Start of the comparison range (inclusive, UTC); set together with <see cref="CompareTo"/>.</summary>
    public DateTime? CompareFrom { get; set; }

    /// <summary>End of the comparison range (exclusive, UTC).</summary>
    public DateTime? CompareTo { get; set; }

    public LoopAnalysisMasks Masks { get; set; } = new();
}

[JsonConverter(typeof(JsonStringEnumConverter<LoopAnalysisComparison>))]
public enum LoopAnalysisComparison
{
    None,

    /// <summary>The period of the same number of days immediately before the primary range.</summary>
    Previous,

    /// <summary>The dates given as <c>compareStartDate</c> and <c>compareEndDate</c>.</summary>
    Custom,
}

/// <summary>A range of tenant-local calendar dates and the instants it resolved to.</summary>
public class LoopAnalysisResolvedRange
{
    /// <summary>First local date.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last local date, inclusive.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>When <see cref="StartDate"/> begins in the tenant zone.</summary>
    public DateTime From { get; set; }

    /// <summary>When the day after <see cref="EndDate"/> begins, exclusive.</summary>
    public DateTime To { get; set; }

    /// <summary>The dates asked for spanned more days than allowed, so only the most recent were kept.</summary>
    public bool Shortened { get; set; }
}

/// <summary>A series or figure the caller's scopes withheld.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LoopAnalysisSeries>))]
public enum LoopAnalysisSeries
{
    Glucose,
    ScheduledBasal,
    DeliveredBasal,
    NetLoopCorrection,

    /// <summary>Manual temp, suspend and inferred insulin above schedule.</summary>
    NetLoopCorrectionComponents,
    HourlyNetLoopCorrection,
    Iob,
    Cob,
    ManualBoluses,
    Carbs,
    Target,
    ProfileNames,

    /// <summary>The summary's insulin per day and its split.</summary>
    InsulinSummary,

    /// <summary>The summary's net loop correction per day and share of total insulin.</summary>
    NetLoopCorrectionSummary,
    DeliveredToScheduledRatio,

    /// <summary>Every prediction curve's accuracy, which compares the APS snapshots with glucose.</summary>
    PredictionCurves,

    /// <summary>How many APS snapshots were analysed, and their algorithms.</summary>
    PredictionCount,

    /// <summary>
    /// The prediction context breakdowns, and the curves reported only in some contexts, which are
    /// drawn from carbs, boluses, overrides and exercise.
    /// </summary>
    PredictionContexts,
}

/// <summary>
/// What every Loop Analysis response says about how its request was read, beside its
/// <c>primary</c> and <c>comparison</c>.
/// </summary>
public class LoopAnalysisResponse
{
    /// <summary>
    /// The caller may read only its last 24 hours, so the range is that window whatever dates were
    /// asked for, and there is no comparison.
    /// </summary>
    public bool HistoryClamped { get; set; }

    /// <summary>
    /// Why no tenant timezone resolved. When set, the dates could not be placed: there are no
    /// resolved ranges, and no range carries slots or days.
    /// </summary>
    public TimeZoneUnavailableReason? TimeZoneUnavailableReason { get; set; }

    /// <summary>The primary range as resolved; null without a timezone.</summary>
    public LoopAnalysisResolvedRange? PrimaryRange { get; set; }

    /// <summary>The comparison range as resolved; null when there is none.</summary>
    public LoopAnalysisResolvedRange? ComparisonRange { get; set; }

    /// <summary>The masks the analysis applied, after the defaults and the caller's scopes.</summary>
    public LoopAnalysisMasks AppliedFilters { get; set; } = new();

    /// <summary>
    /// Query parameters the caller sent but could not use: a profile name without therapy.read,
    /// the masks drawn from treatments or devices without those scopes, and for a history-clamped
    /// caller the dates and the comparison.
    /// </summary>
    public List<string> IgnoredFilters { get; set; } = [];

    /// <summary>Series and figures the caller's scopes withheld.</summary>
    public List<LoopAnalysisSeries> WithheldSeries { get; set; } = [];
}
