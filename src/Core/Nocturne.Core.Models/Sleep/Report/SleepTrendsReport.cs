namespace Nocturne.Core.Models.Sleep.Report;

public class SleepTrendsReport
{
    /// <summary>Per-night summaries, oldest first.</summary>
    public IReadOnlyList<SleepNightSummary> Nights { get; set; } = [];
    public SleepTrendsSummary Summary { get; set; } = new();
}
