namespace Nocturne.Core.Models.LoopAnalysis;

/// <summary>
/// The filter every Loop Analysis endpoint takes. Each exclusion masks the matching time, not the
/// whole day it falls on; <see cref="StateSpanCategory.DataExclusion"/> spans are always taken out
/// of glucose whatever is set here.
/// </summary>
public class LoopAnalysisFilter
{
    /// <summary>Start of the range (inclusive, UTC).</summary>
    public DateTime From { get; set; }

    /// <summary>End of the range (exclusive, UTC).</summary>
    public DateTime To { get; set; }

    /// <summary>Start of the comparison range (inclusive, UTC); set together with <see cref="CompareTo"/>.</summary>
    public DateTime? CompareFrom { get; set; }

    /// <summary>End of the comparison range (exclusive, UTC).</summary>
    public DateTime? CompareTo { get; set; }

    /// <summary>Local weekdays to analyse; every day when empty.</summary>
    public List<DayOfWeek>? Days { get; set; }

    public bool ExcludeOverrides { get; set; }

    public bool ExcludeTemporaryTargets { get; set; }

    public bool ExcludeExercise { get; set; }

    public bool ExcludeIllness { get; set; } = true;

    /// <summary>Masks time the pump was in manual mode or the AID algorithm was off.</summary>
    public bool ExcludeOpenLoop { get; set; } = true;

    /// <summary>Limits the analysis to time this profile was active.</summary>
    public string? ProfileName { get; set; }
}
