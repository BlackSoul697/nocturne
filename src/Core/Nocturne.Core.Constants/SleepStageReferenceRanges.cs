namespace Nocturne.Core.Constants;

/// <summary>
/// Adult reference ranges for sleep stage composition, used by the sleep report
/// stage composition panel to contextualise the user's stage percentages.
/// Source: American Academy of Sleep Medicine (AASM) adult norms.
/// </summary>
public static class SleepStageReferenceRanges
{
    /// <summary>Expected deep sleep percentage range (12–23%).</summary>
    public static readonly (double Min, double Max) Deep = (12, 23);

    /// <summary>Expected REM sleep percentage range (20–25%).</summary>
    public static readonly (double Min, double Max) Rem = (20, 25);

    /// <summary>Expected light sleep percentage range (45–55%).</summary>
    public static readonly (double Min, double Max) Light = (45, 55);

    /// <summary>Expected awake percentage range during sleep (0–8%).</summary>
    public static readonly (double Min, double Max) Awake = (0, 8);
}
