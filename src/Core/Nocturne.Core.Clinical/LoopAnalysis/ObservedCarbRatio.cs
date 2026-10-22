using Nocturne.Core.Constants;
using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.Core.Clinical.LoopAnalysis;

/// <summary>
/// Observed carb ratio per local block, from isolated announced meals: grams over the insulin the
/// meal needed, which is the insulin that acted over its window
/// (<see cref="SettingsEvidenceContext.InsulinActed"/>) plus what glucose's change across it says
/// was missing or spare.
/// </summary>
/// <remarks>
/// A meal is one of <see cref="EventAnchors.Meals"/>, isolated as the Meals &amp; events tab isolates
/// one (<see cref="EventAnchors.IsIsolated"/>): no other meal, correction bolus or meal-like climb,
/// announced or not, falls in its window. The glucose change is converted as
/// <see cref="IsfConversion"/> says, so a wrong profile ISF does not pass into the carb ratio.
/// </remarks>
public static class ObservedCarbRatio
{
    /// <summary>A pre-bolus commonly comes up to a quarter of an hour before the plate.</summary>
    public const long WindowBeforeMs = 15 * LocalSlotBinning.MinuteMs;

    /// <summary>Four hours: a mixed meal has absorbed by then, and its bolus mostly acted.</summary>
    public const long WindowAfterMs = 4 * LocalSlotBinning.HourMs;

    /// <summary>
    /// The fastest glucose may move over the quarter-hour before the meal and still count as
    /// stable: 1 mg/dL a minute, where a CGM's trend arrow turns from flat. Faster, and the window's
    /// glucose change is partly a trend already under way.
    /// </summary>
    public const double MaxStartTrendMgdlPerMinute = 1;

    private static readonly SettingsEvidenceRule[] Rules =
    [
        SettingsEvidenceRule.NotIsolated,
        SettingsEvidenceRule.Override,
        SettingsEvidenceRule.TemporaryTarget,
        SettingsEvidenceRule.Exercise,
        SettingsEvidenceRule.Illness,
        SettingsEvidenceRule.Filtered,
        SettingsEvidenceRule.MissingGlucose,
        SettingsEvidenceRule.StartOutOfRange,
        SettingsEvidenceRule.StartUnstable,
        SettingsEvidenceRule.IobUnknown,
        SettingsEvidenceRule.InsulinUncovered,
        SettingsEvidenceRule.TooLittleInsulin,
    ];

    internal static List<CarbRatioEvidenceBlock> Calculate(
        SettingsEvidenceContext context, IsfConversion conversion, BasalBaseline? baseline)
    {
        var blocks = Enum.GetValues<SettingsEvidenceBlock>();
        var tallies = blocks.Select(_ => new RejectionTally(Rules)).ToArray();
        var episodes = blocks.Select(_ => new List<CarbRatioEvidenceEpisode>()).ToArray();

        var carbs = context.Inputs.Series.Carbs;
        var meals = EventAnchors.Meals(carbs);
        long[] isolating =
        [
            .. meals.Select(m => m.Mills)
                .Concat(EventAnchors.CorrectionBoluses(context.Insulin.Doses, carbs).Select(c => c.Mills))
                .Concat(UnannouncedRises.Find(
                    context.Series.Readings, context.Exclusions, carbs, context.Insulin,
                    requireCorroboration: false, context.Series.CobAt).Climbs)
                .Order(),
        ];

        foreach (var meal in meals)
        {
            if (meal.Mills < context.From || meal.Mills >= context.To)
                continue;
            var block = (int)SettingsEvidenceCalculator.BlockOf(LocalSlotBinning.LocalTime(meal.Mills, context.Zone).Hour);
            tallies[block].Candidate();
            var (episode, rule) = EventAnchors.IsIsolated(meal, isolating, WindowBeforeMs, WindowAfterMs)
                ? Measure(context, meal, conversion, baseline)
                : (null, SettingsEvidenceRule.NotIsolated);
            if (rule is { } rejected)
                tallies[block].Reject(rejected);
            else
                episodes[block].Add(episode!);
        }

        return [.. blocks.Select((b, i) =>
        {
            var tally = tallies[i];
            return context.Finish(
                new CarbRatioEvidenceBlock { Block = b, Episodes = context.ListEpisodes ? episodes[i] : [] },
                b,
                [.. episodes[i].Select(e => e.Value)],
                tally,
                tally.Candidates == 0 ? SettingsEvidenceUnavailableReason.NoAnnouncedMeals
                    : tally[SettingsEvidenceRule.NotIsolated] == tally.Candidates ? SettingsEvidenceUnavailableReason.NoneIsolated
                    : SettingsEvidenceCalculator.Unavailable(tally.Candidates, episodes[i].Count),
                t => context.TherapyAt(t).CarbRatioAt(t),
                baseline,
                episodes[i].Any(e => e.IsfDependent));
        })];
    }

    private static (CarbRatioEvidenceEpisode? Episode, SettingsEvidenceRule? Rejected) Measure(
        SettingsEvidenceContext context, EventAnchor meal, IsfConversion conversion, BasalBaseline? baseline)
    {
        var start = meal.Mills - WindowBeforeMs;
        var end = meal.Mills + WindowAfterMs;
        if (context.Activity(start, end, openLoop: false) is { } activity)
            return (null, activity);
        if (context.Filtered(start, end))
            return (null, SettingsEvidenceRule.Filtered);
        if (context.GlucoseAt(start) is not { } startGlucose
            || context.GlucoseAt(meal.Mills) is not { } mealGlucose
            || context.GlucoseAt(end) is not { } endGlucose)
            return (null, SettingsEvidenceRule.MissingGlucose);
        if (startGlucose is < GlucoseConstants.TargetBottomMgdl or > GlucoseConstants.TargetTopMgdl)
            return (null, SettingsEvidenceRule.StartOutOfRange);
        if (Math.Abs(mealGlucose - startGlucose) > MaxStartTrendMgdlPerMinute * WindowBeforeMs / LocalSlotBinning.MinuteMs)
            return (null, SettingsEvidenceRule.StartUnstable);
        var (acted, uncovered) = context.InsulinActed(start, end, baseline);
        if (acted is not { } insulin)
            return (null, uncovered);

        var (isf, isfDependent) = conversion.At(context, meal.Mills);
        var needed = insulin.Acted + (endGlucose - startGlucose) / isf;
        if (needed < SettingsEvidenceCalculator.MinimumInsulinUnits)
            return (null, SettingsEvidenceRule.TooLittleInsulin);

        var grams = meal.Grams!.Value;
        var episode = context.Episode<CarbRatioEvidenceEpisode>(
            meal.Mills, end, grams / needed, context.TherapyAt(meal.Mills).CarbRatioAt(meal.Mills));
        context.Fill(episode, startGlucose, endGlucose, insulin);
        episode.Grams = SlotSummary.Round(grams);
        episode.InsulinNeeded = SlotSummary.Round(needed);
        episode.Isf = SlotSummary.Round(isf);
        episode.IsfDependent = isfDependent;
        return (episode, null);
    }
}
