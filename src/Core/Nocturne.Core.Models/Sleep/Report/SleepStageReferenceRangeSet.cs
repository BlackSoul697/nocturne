using Nocturne.Core.Constants;

namespace Nocturne.Core.Models.Sleep.Report;

public class SleepStageReferenceRangeSet
{
    public double DeepMin { get; init; }
    public double DeepMax { get; init; }
    public double RemMin { get; init; }
    public double RemMax { get; init; }
    public double LightMin { get; init; }
    public double LightMax { get; init; }
    public double AwakeMin { get; init; }
    public double AwakeMax { get; init; }

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
