using Nocturne.Core.Constants;

namespace Nocturne.Core.Models.Sleep.Report;

public class SleepStageReferenceRangeSet
{
    public double DeepMin { get; set; }
    public double DeepMax { get; set; }
    public double RemMin { get; set; }
    public double RemMax { get; set; }
    public double LightMin { get; set; }
    public double LightMax { get; set; }
    public double AwakeMin { get; set; }
    public double AwakeMax { get; set; }

    public static readonly SleepStageReferenceRangeSet Default = new()
    {
        DeepMin  = SleepStageReferenceRanges.Deep.Min,
        DeepMax  = SleepStageReferenceRanges.Deep.Max,
        RemMin   = SleepStageReferenceRanges.Rem.Min,
        RemMax   = SleepStageReferenceRanges.Rem.Max,
        LightMin = SleepStageReferenceRanges.Light.Min,
        LightMax = SleepStageReferenceRanges.Light.Max,
        AwakeMin = SleepStageReferenceRanges.Awake.Min,
        AwakeMax = SleepStageReferenceRanges.Awake.Max,
    };
}
