using Nocturne.API.Services.Profiles.Resolvers;
using Nocturne.Core.Clinical.LoopAnalysis;
using Nocturne.Core.Contracts.Profiles.Resolvers;
using Nocturne.Core.Models;
using Nocturne.Core.Models.LoopAnalysis;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.Analytics;

/// <remarks>
/// Loop health reads loop cycles over the same lookback and evidence window as the temp basals, so
/// a loop gap that straddles either end of a range is bounded by the cycles on both sides of it.
/// </remarks>
internal sealed partial class LoopAnalysisService
{
    public async Task<LoopHealthResponse> GetLoopHealthAsync(
        LoopAnalysisFilter filter, TimeZoneInfo zone, CancellationToken ct = default)
    {
        var (primary, comparison) = await ForRangesAsync(
            filter,
            window => LoadLoopHealthAsync(window, zone, ct),
            (loaded, window) => CalculateLoopHealth(loaded, filter, window));
        return new LoopHealthResponse { Primary = primary, Comparison = comparison };
    }

    private sealed record LoopHealthLoaded(
        LoopHealthInputs Inputs,
        LoopAnalysisMaskSources Sources,
        IReadOnlyList<PatientDevice> Devices,
        IReadOnlyList<TempBasal> TempBasals);

    private async Task<LoopHealthLoaded> LoadLoopHealthAsync(Window window, TimeZoneInfo zone, CancellationToken ct)
    {
        var to = window.To;
        var cyclesTask = apsSnapshotRepository.GetLoopCyclesAsync(
            ToUtc(window.From - DailyProfileCalculator.ActingMs), ToUtc(to + NetInsulinAboveSchedule.MaxInferredGapMs), ct);
        var commonTask = LoadCommonAsync(window, ct);
        await Task.WhenAll(cyclesTask, commonTask);

        var cycles = await cyclesTask;
        var snapshotMills = cycles.Select(c => c.Mills).ToList();
        var common = await commonTask;
        var delivery = await DeliveryAsync(common, window, snapshotMills, ct);

        var inputs = new LoopHealthInputs
        {
            Zone = zone,
            Cycles = cycles,
            Insulin = delivery.Insulin,
            Evidence = delivery.Evidence,
            Scheduled = delivery.Scheduled,
            Glucose = common.Glucose,
            LoopMaximumBasalRate = cycles.Any(c => c.AidAlgorithm == AidAlgorithm.Loop)
                ? await LoopMaximumBasalRateAsync(common.Profiles, to, ct)
                : null,
        };

        return new LoopHealthLoaded(inputs, Sources(common, snapshotMills, zone, to), common.Devices, common.TempBasals);
    }

    /// <summary>
    /// <see cref="LoopHealthCalculator"/> over the window, with the overall activity percentages from
    /// <see cref="AidSystemMetricsReader"/> over the same records, unmasked.
    /// </summary>
    private LoopHealthRange CalculateLoopHealth(LoopHealthLoaded loaded, LoopAnalysisFilter filter, Window window)
    {
        var inputs = loaded.Inputs;
        var range = LoopHealthCalculator.Calculate(
            inputs,
            LoopAnalysisMask.Build(filter.Masks, window.From, window.To, inputs.Zone, loaded.Sources),
            window.From,
            window.To);

        var start = ToUtc(window.From);
        var end = ToUtc(window.To);
        var overall = aidSystemMetrics.Calculate(
            new AidSystemMetricsRecords(
                loaded.Devices,
                [.. inputs.Cycles.Where(c => c.Timestamp >= start && c.Timestamp < end)],
                [.. loaded.TempBasals.Where(t => t.StartTimestamp < end && (t.EndTimestamp ?? t.StartTimestamp) >= start)],
                [],
                [.. inputs.Glucose.Where(g => g.Mills >= window.From && g.Mills < window.To)],
                null,
                null),
            start,
            end);
        range.Activity.AidActivePercent = overall.AidActivePercent;
        range.Activity.PumpUsePercent = overall.PumpUsePercent;
        range.Activity.CgmActivePercent = overall.CgmActivePercent;
        return range;
    }

    /// <summary>
    /// Loop's maximum basal rate setting over time: the version of each active profile's settings
    /// in force, read once per profile, where it records one.
    /// </summary>
    private async Task<StepTimeline<double>> LoopMaximumBasalRateAsync(
        IReadOnlyList<ProfileSpan> profiles, long to, CancellationToken ct)
    {
        var versions = new Dictionary<string, TherapySettings[]>(StringComparer.Ordinal);
        foreach (var name in profiles.Select(p => p.ProfileName).Distinct(StringComparer.Ordinal))
            versions[name] = [.. (await therapySettingsRepository.GetByProfileNameAsync(name, ct))
                .OrderByDescending(v => v.Timestamp)];

        var steps = new List<Step<double>>();
        foreach (var profile in profiles)
        {
            var start = profile.StartMills;
            var end = profile.EndMills ?? to;
            var bounds = versions[profile.ProfileName]
                .Select(v => ToMills(DateTime.SpecifyKind(v.Timestamp, DateTimeKind.Utc)))
                .Where(m => m > start && m < end)
                .Append(start)
                .Append(end)
                .Distinct()
                .Order()
                .ToList();
            for (var i = 0; i + 1 < bounds.Count; i++)
            {
                var settings = ProfileVersionCache.ActiveAt(versions[profile.ProfileName], ToUtc(bounds[i]));
                if (settings?.LoopSettings?.MaximumBasalRatePerHour is { } maximum)
                    steps.Add(new Step<double>(bounds[i], bounds[i + 1], maximum));
            }
        }
        return StepTimeline<double>.LatestStartedWins(steps);
    }
}
