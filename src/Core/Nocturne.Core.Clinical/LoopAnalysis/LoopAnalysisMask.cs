using Nocturne.Core.Models;
using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.Core.Clinical.LoopAnalysis;

/// <summary>The time each <see cref="LoopAnalysisFilter"/> exclusion can take out of a range.</summary>
public sealed record LoopAnalysisMaskSources
{
    public IReadOnlyList<MillsInterval> Overrides { get; init; } = [];
    public IReadOnlyList<MillsInterval> TemporaryTargets { get; init; } = [];
    public IReadOnlyList<MillsInterval> Exercise { get; init; } = [];
    public IReadOnlyList<MillsInterval> Illness { get; init; } = [];
    public IReadOnlyList<MillsInterval> OpenLoop { get; init; } = [];

    /// <summary><see cref="StateSpanCategory.DataExclusion"/> spans: compression lows, sensor errors.</summary>
    public IReadOnlyList<MillsInterval> DataExclusions { get; init; } = [];

    public IReadOnlyList<ProfileInterval> Profiles { get; init; } = [];
}

public readonly record struct ProfileInterval(string ProfileName, long Start, long End);

/// <summary>
/// The time a range is analysed over once a <see cref="LoopAnalysisFilter"/> is applied, which every
/// Loop Analysis calculator consumes.
/// </summary>
/// <param name="Analysed">The range, less the masked time.</param>
/// <param name="Glucose"><paramref name="Analysed"/> less every data exclusion.</param>
public sealed record LoopAnalysisMask(IntervalSet Analysed, IntervalSet Glucose)
{
    public static LoopAnalysisMask Build(
        LoopAnalysisFilter filter, long from, long to, TimeZoneInfo tz, LoopAnalysisMaskSources sources)
    {
        var analysed = IntervalSet.Of(from, to);

        if (filter.Days is { Count: > 0 } days)
            analysed = analysed.Intersect(LocalWeekdays(from, to, tz, days.ToHashSet()));

        if (filter.ProfileName is { } profileName)
            analysed = analysed.Intersect(IntervalSet.Of(sources.Profiles
                .Where(p => string.Equals(p.ProfileName, profileName, StringComparison.Ordinal))
                .Select(p => new MillsInterval(p.Start, p.End))));

        var masked = new List<MillsInterval>();
        if (filter.ExcludeOverrides) masked.AddRange(sources.Overrides);
        if (filter.ExcludeTemporaryTargets) masked.AddRange(sources.TemporaryTargets);
        if (filter.ExcludeExercise) masked.AddRange(sources.Exercise);
        if (filter.ExcludeIllness) masked.AddRange(sources.Illness);
        if (filter.ExcludeOpenLoop) masked.AddRange(sources.OpenLoop);
        analysed = analysed.Subtract(IntervalSet.Of(masked));

        return new LoopAnalysisMask(analysed, analysed.Subtract(IntervalSet.Of(sources.DataExclusions)));
    }

    private static IntervalSet LocalWeekdays(long from, long to, TimeZoneInfo tz, HashSet<DayOfWeek> days)
    {
        var kept = new List<MillsInterval>();
        var last = LocalSlotBinning.LocalDay(to - 1, tz);
        for (var day = LocalSlotBinning.LocalDay(from, tz); day <= last; day = day.AddDays(1))
        {
            if (days.Contains(day.DayOfWeek))
                kept.Add(new MillsInterval(
                    LocalSlotBinning.DayStartMills(day, tz), LocalSlotBinning.DayStartMills(day.AddDays(1), tz)));
        }
        return IntervalSet.Of(kept);
    }
}
