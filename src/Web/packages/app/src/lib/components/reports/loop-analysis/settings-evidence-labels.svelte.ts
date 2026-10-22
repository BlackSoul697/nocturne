import {
  type SettingsEvidenceRules,
  SettingsEvidenceConfidence,
  SettingsEvidenceConfidenceReason,
  SettingsEvidenceRule,
  SettingsEvidenceUnavailableReason,
} from "$lib/api";
import { bg, bgLabel } from "$lib/utils/formatting";
import { plural } from "$lib/utils/plural";

const HOUR_MS = 3_600_000;

/** A rule's span in hours, as it reads. */
export function hoursText(ms: number | undefined): string {
  return plural((ms ?? 0) / HOUR_MS, ["# hour", "# hours"]);
}

/** Built per call so wuchale reads them in the current locale. */
export function confidenceLabel(confidence: SettingsEvidenceConfidence | undefined): string {
  switch (confidence) {
    case SettingsEvidenceConfidence.High:
      return "High confidence";
    case SettingsEvidenceConfidence.Moderate:
      return "Moderate confidence";
    default:
      return "Low confidence";
  }
}

/** Why a figure is less sure than it could be, and what would make it surer. */
export function reasonText(reason: SettingsEvidenceConfidenceReason | undefined): { why: string; raise: string } {
  switch (reason) {
    case SettingsEvidenceConfidenceReason.FewEpisodes:
      return {
        why: "Only a small number of episodes give this figure.",
        raise: "Confidence increases when the range has more episodes.",
      };
    case SettingsEvidenceConfidenceReason.WideSpread:
      return {
        why: "The episodes are very different from each other.",
        raise: "Confidence increases when more episodes show if the figure becomes stable.",
      };
    case SettingsEvidenceConfidenceReason.IsfDependent:
      return {
        why: "The glucose changes were converted with the ISF from the profile, not with an ISF from your data.",
        raise: "Confidence increases when your data has correction boluses at this time of day, given when glucose was steady and no carbs were logged near them.",
      };
    case SettingsEvidenceConfidenceReason.BasalDependent:
      return {
        why: "The insulin was measured against basal that was not checked for this time of day.",
        raise: "Confidence increases when your data has times without food at this time of day.",
      };
    case SettingsEvidenceConfidenceReason.ResidualIob:
      return {
        why: "Much of the insulin was still active at the end of the window, so the figure depends on the insulin curve.",
        raise: "Confidence increases when your data has episodes with less insulin still active at their end.",
      };
    case SettingsEvidenceConfidenceReason.MostNotIsolated:
      return {
        why: "Most candidates overlapped other meals, boluses or rises. The few that did not overlap are possibly not typical.",
        raise: "Confidence increases when your data has more periods without overlaps.",
      };
    case SettingsEvidenceConfidenceReason.FastingUnverifiable:
      return {
        why: "Carbs are logged on only a few days, so the fasting time comes from glucose only.",
        raise: "Confidence increases when your data has carbs logged on more days, so that the fasting time can come from meals.",
      };
    case SettingsEvidenceConfidenceReason.MoreInsulinOnBorrowedInput:
      return {
        why: "This figure points to more insulin. Part of it depends on something that this block cannot check by itself: another figure, the profile, or the estimate of the insulin curve.",
        raise: "Confidence increases with evidence from this block only.",
      };
    case SettingsEvidenceConfidenceReason.StillMovingAtEnd:
      return {
        why: "Glucose usually still changed at the end of the window, so the figure does not include all of the response.",
        raise: "Confidence increases when your data has episodes where glucose becomes stable before the end of the window.",
      };
    case SettingsEvidenceConfidenceReason.CarriedInsulinMaterial:
      return {
        why: "Much of the figure depends on insulin carried in from earlier, as the insulin curve estimates it.",
        raise: "Confidence increases when your data has times without food that start with little insulin still active.",
      };
    default:
      return { why: "", raise: "" };
  }
}

export type SettingsEvidenceSection = "basal" | "isf" | "carbRatio";

/** Why a block shows no figure, in the words of the section it is in. */
export function unavailableText(
  reason: SettingsEvidenceUnavailableReason | undefined,
  section: SettingsEvidenceSection
): string {
  switch (reason) {
    case SettingsEvidenceUnavailableReason.NoCandidates:
      return section === "isf" ? "No correction boluses in this block" : "No time in this block in the range";
    case SettingsEvidenceUnavailableReason.NoAnnouncedMeals:
      return "No meals were logged with carbs in this block";
    case SettingsEvidenceUnavailableReason.NoneIsolated:
      return "Every meal in this block overlapped another meal, bolus or rise";
    case SettingsEvidenceUnavailableReason.NoneQualified:
      return "All candidates were set aside. Open the block to see why.";
    case SettingsEvidenceUnavailableReason.TooFew:
      return "Too few episodes to show a figure";
    case SettingsEvidenceUnavailableReason.SteepTrend:
      return "Glucose changed too much in this block to calculate basal from it";
    case SettingsEvidenceUnavailableReason.Inconsistent:
      return "The loop adjusted in different directions on different days";
    default:
      return "Nothing to show";
  }
}

/** Why candidates were set aside, one rule each. */
export function ruleText(rule: SettingsEvidenceRule | undefined, rules: SettingsEvidenceRules | undefined): string {
  switch (rule) {
    case SettingsEvidenceRule.OpenLoop:
      return "The loop was open";
    case SettingsEvidenceRule.Override:
      return "An override was running";
    case SettingsEvidenceRule.TemporaryTarget:
      return "A temporary target was set";
    case SettingsEvidenceRule.Exercise:
      return "Exercise was logged";
    case SettingsEvidenceRule.Illness:
      return "Illness was logged";
    case SettingsEvidenceRule.Filtered:
      return "Outside the report's filters";
    case SettingsEvidenceRule.OutsideRange:
      return "Not fully inside the range";
    case SettingsEvidenceRule.CarbsBefore:
      return `Carbs within ${hoursText(rules?.basalCarbFreeMs)} before, or during`;
    case SettingsEvidenceRule.RiseBefore:
      return `Glucose rose as after a meal, within ${hoursText(rules?.basalCarbFreeMs)} before or during`;
    case SettingsEvidenceRule.ManualBolus:
      return `A manual bolus within ${hoursText(rules?.basalManualBolusFreeMs)} before, or during`;
    case SettingsEvidenceRule.CarbsOnBoard:
      return "Carbs still on board";
    case SettingsEvidenceRule.ManualDelivery:
      return "A manual temp basal or a suspend";
    case SettingsEvidenceRule.InferredDelivery:
      return "Basal delivery was not recorded and differed from the schedule";
    case SettingsEvidenceRule.CarbsNear:
      return `Carbs from ${hoursText(rules?.isfCarbFreeBeforeMs)} before to ${hoursText(rules?.isfMinimumWindowMs)} after`;
    case SettingsEvidenceRule.NoTarget:
      return "No target range was set";
    case SettingsEvidenceRule.NotAboveTarget:
      return "Glucose was not above the target range";
    case SettingsEvidenceRule.IobAtBolus:
      return "The active insulin was already more than the bolus";
    case SettingsEvidenceRule.IobUnknown:
      return "Insulin on board is not known";
    case SettingsEvidenceRule.IobSourceChanged:
      return "Insulin on board came from different sources at the start and end";
    case SettingsEvidenceRule.WentLow:
      return `Glucose went below ${bg(rules?.lowMgdl ?? 0)} ${bgLabel()}`;
    case SettingsEvidenceRule.NotIsolated:
      return "Another meal, correction or rise in the window";
    case SettingsEvidenceRule.StartOutOfRange:
      return `Glucose was outside ${bg(rules?.startLowMgdl ?? 0)}–${bg(rules?.startHighMgdl ?? 0)} ${bgLabel()} at the start`;
    case SettingsEvidenceRule.StartUnstable:
      return "Glucose was moving at the start";
    case SettingsEvidenceRule.MissingGlucose:
      return "Glucose readings were missing";
    case SettingsEvidenceRule.InsulinUncovered:
      return "The pump's records were incomplete";
    case SettingsEvidenceRule.TooLittleInsulin:
      return "Too little insulin to calculate a ratio";
    default:
      return "Set aside";
  }
}
