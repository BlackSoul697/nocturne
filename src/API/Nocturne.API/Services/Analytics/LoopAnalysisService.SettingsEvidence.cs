using Nocturne.Core.Clinical.LoopAnalysis;
using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.API.Services.Analytics;

internal sealed partial class LoopAnalysisService
{
    /// <summary>
    /// How far before the range the Settings evidence load reads. A basal block is rejected for a
    /// meal-like climb that peaked within <see cref="ObservedBasalNeed.CarbFreeMs"/> before it; such a
    /// climb starts up to <see cref="UnannouncedRises.MergeMs"/> before its peak, is found only after
    /// a <see cref="UnannouncedRises.StableMs"/> stretch before that, and is told from an announced meal
    /// by carbs up to <see cref="UnannouncedRises.CarbsBeforeMs"/> before its start. That reaches past
    /// <see cref="ObservedBasalNeed.ManualBolusFreeMs"/> and every window's own look back.
    /// </summary>
    private const long SettingsEvidenceBeforeMs =
        ObservedBasalNeed.CarbFreeMs + UnannouncedRises.MergeMs + UnannouncedRises.StableMs + UnannouncedRises.CarbsBeforeMs;

    /// <summary>
    /// How far past the range it reads: a meal or correction near the range's end reads its window,
    /// <see cref="ObservedCarbRatio.WindowAfterMs"/>, past it, and <see cref="EventMarginMs"/> more
    /// lets a climb starting near that window's end be seen for what it is.
    /// </summary>
    private const long SettingsEvidenceAfterMs = ObservedCarbRatio.WindowAfterMs + EventMarginMs;

    public async Task<SettingsEvidenceReport> GetSettingsEvidenceAsync(
        LoopAnalysisFilter filter, TimeZoneInfo zone, CancellationToken ct = default)
    {
        var (primary, comparison, names) = await ForRangesAsync(
            filter,
            range => LoadSettingsEvidenceAsync(range, zone, ct),
            (loaded, range, isPrimary) => SettingsEvidenceCalculator.Calculate(
                loaded.Inputs, Mask(filter, range, zone, loaded.Sources), range.From, range.To, listEpisodes: isPrimary));
        return new SettingsEvidenceReport { Primary = primary, Comparison = comparison, ProfileNames = names };
    }

    private sealed record SettingsEvidenceLoaded(SettingsEvidenceInputs Inputs) : ILoaded
    {
        public LoopAnalysisMaskSources Sources => Inputs.Sources;
    }

    /// <summary>
    /// The Daily profile's reads over <paramref name="range"/> widened by
    /// <see cref="SettingsEvidenceBeforeMs"/> and <see cref="SettingsEvidenceAfterMs"/>, with the
    /// therapy timeline they were read against.
    /// </summary>
    private async Task<SettingsEvidenceLoaded> LoadSettingsEvidenceAsync(Window range, TimeZoneInfo zone, CancellationToken ct)
    {
        var padded = new Window(range.From - SettingsEvidenceBeforeMs, range.To + SettingsEvidenceAfterMs);
        var loaded = await LoadDailyProfileAsync(padded, zone, ct);
        return new SettingsEvidenceLoaded(new SettingsEvidenceInputs
        {
            Series = loaded.Inputs,
            Sources = loaded.Sources,
            Therapy = loaded.Timeline,
        });
    }
}
