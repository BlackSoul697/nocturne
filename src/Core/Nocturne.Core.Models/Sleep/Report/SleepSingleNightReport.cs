using Nocturne.Core.Models;

namespace Nocturne.Core.Models.Sleep.Report;

public class SleepSingleNightReport
{
    public SleepSession Session { get; set; } = null!;
    public SleepScoreSource ScoreSource { get; set; }
    public SleepStageBreakdown StageBreakdown { get; set; } = null!;
    /// <summary>Null when no CGM data overlaps the session window.</summary>
    public SleepOvernightTir? OvernightTir { get; set; }
    public IReadOnlyList<SleepHypoEvent> HypoEvents { get; set; } = [];
    /// <summary>Null when fewer than 4 CGM readings exist in the final 2-hour pre-wake window.</summary>
    public SleepDawnPhenomenon? DawnPhenomenon { get; set; }
    public IReadOnlyList<SleepWakeEvent> WakeEvents { get; set; } = [];
}
