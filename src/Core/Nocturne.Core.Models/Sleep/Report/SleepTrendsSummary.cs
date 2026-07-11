namespace Nocturne.Core.Models.Sleep.Report;

public class SleepTrendsSummary
{
    public int NightCount { get; set; }
    /// <summary>Calendar days in the requested range, inclusive of both endpoints.</summary>
    public int DaysInRange { get; set; }
    /// <summary>NightCount / DaysInRange as a percentage, clamped to 100. 0 when DaysInRange is 0.</summary>
    public double CoveragePct { get; set; }
    public double? MeanScore { get; set; }
    public double? MeanTirPct { get; set; }
    public double MeanAsleepMinutes { get; set; }
    public double MeanDeepPct { get; set; }
    public double MeanRemPct { get; set; }
    public double? MeanDawnRiseMg { get; set; }
    public double? MeanHrvMs { get; set; }
    public double NightsWithHypoPct { get; set; }
    public int TotalHypoCount { get; set; }
    public SleepTrendsDelta Last7dVsPrior7d { get; set; } = new();
    /// <summary>Adult reference ranges from AASM norms, included so the frontend never hardcodes thresholds.</summary>
    public SleepStageReferenceRangeSet ReferenceRanges { get; set; } = SleepStageReferenceRangeSet.Default;
}
