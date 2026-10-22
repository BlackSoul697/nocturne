import {
  SettingsEvidenceConfidence,
  SettingsEvidenceConfidenceReason,
  SettingsEvidenceRule,
  SettingsEvidenceUnavailableReason,
} from "$lib/api";
import { bg, bgLabel } from "$lib/utils/formatting";

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
      return { why: "Few episodes are behind it.", raise: "A longer range would add more." };
    case SettingsEvidenceConfidenceReason.WideSpread:
      return {
        why: "The episodes disagree widely with one another.",
        raise: "More episodes would show whether the figure settles.",
      };
    case SettingsEvidenceConfidenceReason.IsfDependent:
      return {
        why: "Glucose changes were converted with the profile's ISF, not one seen in your data.",
        raise: "Correction boluses in this block, given with glucose steady and nothing eaten, would let us use your own.",
      };
    case SettingsEvidenceConfidenceReason.BasalDependent:
      return {
        why: "The insulin was measured against basal that was not checked for this time of day.",
        raise: "A fasting stretch in this block would let us check basal here.",
      };
    case SettingsEvidenceConfidenceReason.ResidualIob:
      return {
        why: "Much of the insulin was still active when the window ended, so the insulin curve carries the figure.",
        raise: "Episodes with less insulin still active at their end would make it firmer.",
      };
    case SettingsEvidenceConfidenceReason.MostNotIsolated:
      return {
        why: "Most candidates overlapped other meals, boluses or rises, so the few that did not may not be typical.",
        raise: "More stretches without overlaps would show whether these are.",
      };
    case SettingsEvidenceConfidenceReason.FastingUnverifiable:
      return {
        why: "Carbs are logged on few days, so fasting time is told from glucose alone.",
        raise: "Logging carbs would let us tell fasting time from meals.",
      };
    case SettingsEvidenceConfidenceReason.MoreInsulinOnBorrowedInput:
      return {
        why: "It points towards more insulin, and part of it comes from another figure or the profile.",
        raise: "Evidence from this block on its own would make it firmer.",
      };
    case SettingsEvidenceConfidenceReason.StillMovingAtEnd:
      return {
        why: "Glucose was usually still moving when the window closed, so part of the response was missed.",
        raise: "Episodes that settle within four hours would make it firmer.",
      };
    case SettingsEvidenceConfidenceReason.CarriedInsulinMaterial:
      return {
        why: "Much of it rests on insulin carried in from earlier, as the insulin curve estimates it.",
        raise: "Fasting stretches that start with less insulin active would make it firmer.",
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
      return "Every candidate was set aside; the reasons are listed below";
    case SettingsEvidenceUnavailableReason.TooFew:
      return "Too few episodes to show a figure";
    case SettingsEvidenceUnavailableReason.SteepTrend:
      return "Glucose moved too much across this block to read basal from it";
    case SettingsEvidenceUnavailableReason.Inconsistent:
      return "The loop adjusted in different directions on different days";
    default:
      return "Nothing to show";
  }
}

/** Why candidates were set aside, one rule each. */
export function ruleText(rule: SettingsEvidenceRule | undefined): string {
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
      return "Ran past the edge of the range";
    case SettingsEvidenceRule.CarbsBefore:
      return "Carbs within 4 hours before, or during";
    case SettingsEvidenceRule.RiseBefore:
      return "Glucose rose as after a meal, within 4 hours before or during";
    case SettingsEvidenceRule.ManualBolus:
      return "A manual bolus within 5 hours before, or during";
    case SettingsEvidenceRule.CarbsOnBoard:
      return "Carbs still on board";
    case SettingsEvidenceRule.ManualDelivery:
      return "A manual temp basal or a suspend";
    case SettingsEvidenceRule.InferredDelivery:
      return "Basal delivery was not recorded and differed from the schedule";
    case SettingsEvidenceRule.CarbsNear:
      return "Carbs from an hour before to three hours after";
    case SettingsEvidenceRule.NoTarget:
      return "No target range was set";
    case SettingsEvidenceRule.NotAboveTarget:
      return "Glucose was not above the target range";
    case SettingsEvidenceRule.IobAtBolus:
      return "Much insulin was already active";
    case SettingsEvidenceRule.IobUnknown:
      return "Insulin on board is not known";
    case SettingsEvidenceRule.IobSourceChanged:
      return "Insulin on board came from different sources at the start and end";
    case SettingsEvidenceRule.WentLow:
      return `Glucose went below ${bg(70)} ${bgLabel()}`;
    case SettingsEvidenceRule.NotIsolated:
      return "Another meal, correction or rise in the window";
    case SettingsEvidenceRule.StartOutOfRange:
      return "Glucose was out of range at the start";
    case SettingsEvidenceRule.StartUnstable:
      return "Glucose was moving at the start";
    case SettingsEvidenceRule.MissingGlucose:
      return "Glucose readings were missing";
    case SettingsEvidenceRule.InsulinUncovered:
      return "The pump's records were incomplete";
    case SettingsEvidenceRule.TooLittleInsulin:
      return "Too little insulin to read a ratio from";
    default:
      return "Set aside";
  }
}
