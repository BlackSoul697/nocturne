import { resolve } from "$app/paths";

export const LOOP_ANALYSIS_PAGES = [
  "daily-profile",
  "meals-and-events",
  "prediction-accuracy",
  "loop-health",
  "settings-evidence",
] as const;

export type LoopAnalysisPage = (typeof LOOP_ANALYSIS_PAGES)[number];

/** The page each `tab` of the one-page report opened, for links made before the pages split. */
export const LEGACY_TABS: Record<string, LoopAnalysisPage> = {
  "daily-profile": "daily-profile",
  "meals-events": "meals-and-events",
  predictions: "prediction-accuracy",
  "loop-health": "loop-health",
  "settings-evidence": "settings-evidence",
};

/** One line under the header of each page, built per call so wuchale reads it in the current locale. */
export function loopAnalysisPageIntros(): Record<LoopAnalysisPage, string> {
  return {
    "daily-profile":
      "This page shows your glucose, basal, loop corrections, insulin on board (IOB) and carbs on board (COB) at each time of day.",
    "meals-and-events":
      "This page aligns your glucose and insulin on meals, corrections and other events. It shows the time before and after each event.",
    "prediction-accuracy":
      "This page compares the glucose predictions of the loop with the glucose readings that followed them.",
    "loop-health":
      "This page shows how much of the time the loop was closed, why it stopped and how frequently it reached its limits.",
    "settings-evidence":
      "This page compares your profile settings with what your data shows. Speak to your care team before you change a setting.",
  };
}

/** The resolved path of a Loop Analysis page, with the query string the report is shown with. */
export function loopAnalysisPath(page: LoopAnalysisPage, search: URLSearchParams, hash = ""): string {
  const query = search.toString();
  return `${resolve(`/reports/loop-analysis/${page}`)}${query ? `?${query}` : ""}${hash ? `#${hash}` : ""}`;
}
