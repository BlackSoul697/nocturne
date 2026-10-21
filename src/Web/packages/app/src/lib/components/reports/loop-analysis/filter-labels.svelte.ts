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

/** A name from a response's `ignoredFilters`, as the filter bar labels it. */
export function ignoredFilterLabel(name: string): string {
  if (name === "profileName") return "Profile";
  const masks: Record<string, string> = maskLabels();
  return masks[name] ?? name;
}
