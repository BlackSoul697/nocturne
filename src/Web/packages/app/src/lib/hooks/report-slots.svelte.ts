import { getContext, onDestroy, setContext, type Snippet } from "svelte";

/**
 * Filters a report adds to the reports layout's Filters panel, below the date range. The fields
 * edit a draft the report owns; the panel asks the report to start the draft when it opens and to
 * write it when Apply or Reset is used.
 */
export interface ReportFilterExtension {
  fields: Snippet;
  /** Copy the filters in force into the draft. */
  begin: () => void;
  /** Write the draft into the URL. The panel waits for it before it writes the date range. */
  apply: () => Promise<void>;
  /** Clear the report's filters from the URL. */
  reset: () => Promise<void>;
}

/** What a report adds to the reports layout. */
export class ReportSlots {
  filters = $state.raw<ReportFilterExtension | null>(null);
}

// A string for the reason given at LOOP_ANALYSIS_KEY in loop-analysis-context.svelte.ts.
const REPORT_SLOTS_KEY = "report-slots";

/** Call from the reports layout. */
export function createReportSlots(): ReportSlots {
  const slots = new ReportSlots();
  setContext(REPORT_SLOTS_KEY, slots);
  return slots;
}

export function getReportSlots(): ReportSlots | undefined {
  return getContext<ReportSlots | undefined>(REPORT_SLOTS_KEY);
}

/** Adds `extension` to the Filters panel for as long as the calling component is mounted. */
export function useReportFilters(extension: ReportFilterExtension): void {
  const slots = getReportSlots();
  if (!slots) return;
  slots.filters = extension;
  onDestroy(() => {
    if (slots.filters === extension) slots.filters = null;
  });
}
