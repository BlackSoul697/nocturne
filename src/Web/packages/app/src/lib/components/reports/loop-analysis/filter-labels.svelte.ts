import type { MaskKey } from "./loop-analysis-filter";

/** Built per call so wuchale reads them in the current locale. */
export function maskLabels(): Record<MaskKey, string> {
  return {
    excludeOverrides: "Overrides",
    excludeTemporaryTargets: "Temporary targets",
    excludeExercise: "Exercise",
    excludeIllness: "Illness",
    excludeOpenLoop: "Time not in closed loop",
  };
}

/** Why a filter is unavailable, by the read scope it needs. */
export function missingScopeReason(scope: string): string {
  switch (scope) {
    case "treatments.read":
      return "Needs access to treatments";
    case "devices.read":
      return "Needs access to devices";
    case "therapy.read":
      return "Needs access to therapy settings";
    default:
      return "Not available with your access";
  }
}

/** A name from a response's `ignoredFilters`, as the filter bar and the event builder label it. */
export function ignoredFilterLabel(name: string): string {
  if (name === "profileName") return "Profile";
  const cohort = /^anchor(\d)$/.exec(name);
  if (cohort) return `Cohort ${cohort[1]}`;
  if (name === "isolatedOnly") return "Isolated events only";
  if (name === "requireCorroboration") return "Only rises the loop answered";
  const masks: Record<string, string> = maskLabels();
  return masks[name] ?? name;
}
