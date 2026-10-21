import { parseDate } from "@internationalized/date";
import { DayOfWeek } from "$lib/api";
import type { getDailyProfile } from "$api/generated/loopAnalysis.generated.remote";
import { isDayString, startOfDay } from "$lib/utils/date-range";

type DailyProfileQuery = Parameters<typeof getDailyProfile>[0];

export const LOOP_ANALYSIS_TABS = ["daily-profile"] as const;
export type LoopAnalysisTab = (typeof LOOP_ANALYSIS_TABS)[number];

export type CompareMode = "none" | "previous" | "custom";

export type LoopAnalysisMasks = Required<
  Pick<
    DailyProfileQuery,
    | "excludeOverrides"
    | "excludeTemporaryTargets"
    | "excludeExercise"
    | "excludeIllness"
    | "excludeOpenLoop"
  >
>;

export type MaskKey = keyof LoopAnalysisMasks;

/**
 * The report's filter as the URL carries it, beside the shared report range (`days`/`from`/`to`),
 * whose names it must not reuse: the weekdays travel as `weekdays`.
 */
export interface LoopAnalysisFilterState extends LoopAnalysisMasks {
  tab: LoopAnalysisTab;
  compare: CompareMode;
  /** yyyy-MM-dd, inclusive; read only when {@link compare} is custom. */
  compareFrom: string | null;
  compareTo: string | null;
  weekdays: DayOfWeek[];
  profileName: string | null;
}

/** The API's own defaults (`LoopAnalysisController.GetDailyProfile`). */
export const DEFAULT_MASKS: LoopAnalysisMasks = {
  excludeOverrides: false,
  excludeTemporaryTargets: false,
  excludeExercise: false,
  excludeIllness: true,
  excludeOpenLoop: true,
};

export const MASK_KEYS: MaskKey[] = [
  "excludeOverrides",
  "excludeTemporaryTargets",
  "excludeExercise",
  "excludeIllness",
  "excludeOpenLoop",
];

export function isLoopAnalysisTab(value: string): value is LoopAnalysisTab {
  return LOOP_ANALYSIS_TABS.some((tab) => tab === value);
}

export function isCompareMode(value: string): value is CompareMode {
  return value === "none" || value === "previous" || value === "custom";
}

const WEEKDAYS: DayOfWeek[] = [
  DayOfWeek.Sunday,
  DayOfWeek.Monday,
  DayOfWeek.Tuesday,
  DayOfWeek.Wednesday,
  DayOfWeek.Thursday,
  DayOfWeek.Friday,
  DayOfWeek.Saturday,
];

/** Weekdays from their numeric spelling, unknown ones dropped, each once, Sunday first. */
export function toWeekdays(values: readonly (string | number)[]): DayOfWeek[] {
  const picked = new Set(values.map(Number));
  return WEEKDAYS.filter((day) => picked.has(day));
}

export const DEFAULT_FILTER: LoopAnalysisFilterState = {
  tab: "daily-profile",
  compare: "none",
  compareFrom: null,
  compareTo: null,
  weekdays: [],
  profileName: null,
  ...DEFAULT_MASKS,
};

/** Every query parameter this filter owns; a write clears them all before setting its own. */
export const FILTER_PARAM_NAMES = [
  "tab",
  "compare",
  "compareFrom",
  "compareTo",
  "weekdays",
  "profile",
  ...MASK_KEYS,
] as const;

function readBoolean(value: string | null, fallback: boolean): boolean {
  if (value === "true") return true;
  if (value === "false") return false;
  return fallback;
}

export function readLoopAnalysisFilter(search: URLSearchParams): LoopAnalysisFilterState {
  const tab = search.get("tab");
  const compare = search.get("compare");
  const compareFrom = search.get("compareFrom");
  const compareTo = search.get("compareTo");

  const mask = (key: MaskKey) => readBoolean(search.get(key), DEFAULT_MASKS[key]);

  return {
    tab: tab && isLoopAnalysisTab(tab) ? tab : DEFAULT_FILTER.tab,
    compare: compare && isCompareMode(compare) ? compare : "none",
    compareFrom: isDayString(compareFrom) ? compareFrom : null,
    compareTo: isDayString(compareTo) ? compareTo : null,
    weekdays: toWeekdays((search.get("weekdays") ?? "").split(",").filter((part) => part !== "")),
    profileName: search.get("profile") || null,
    excludeOverrides: mask("excludeOverrides"),
    excludeTemporaryTargets: mask("excludeTemporaryTargets"),
    excludeExercise: mask("excludeExercise"),
    excludeIllness: mask("excludeIllness"),
    excludeOpenLoop: mask("excludeOpenLoop"),
  };
}

/**
 * The search params with this filter written into them, leaving every parameter it does not own
 * as it was. A value at its default is left out, so a bare link reads as the defaults.
 */
export function writeLoopAnalysisFilter(
  filter: LoopAnalysisFilterState,
  search: URLSearchParams
): URLSearchParams {
  const next = new URLSearchParams(search);
  for (const name of FILTER_PARAM_NAMES) next.delete(name);

  if (filter.tab !== DEFAULT_FILTER.tab) next.set("tab", filter.tab);
  if (filter.compare !== "none") next.set("compare", filter.compare);
  if (filter.compare === "custom") {
    if (filter.compareFrom) next.set("compareFrom", filter.compareFrom);
    if (filter.compareTo) next.set("compareTo", filter.compareTo);
  }
  if (filter.weekdays.length > 0) next.set("weekdays", toWeekdays(filter.weekdays).join(","));
  if (filter.profileName) next.set("profile", filter.profileName);
  for (const key of MASK_KEYS)
    if (filter[key] !== DEFAULT_MASKS[key]) next.set(key, String(filter[key]));

  return next;
}

const ANALYSIS_SPAN_DAYS = 90;
const DAY_MS = 24 * 60 * 60 * 1000;
const HISTORY_CLAMP_MS = DAY_MS;

function nextDay(day: string): string {
  return parseDate(day).add({ days: 1 }).toString();
}

export interface RangeRequest {
  from: Date;
  to: Date;
  /** The start moved later to stay within the 90 days an analysis may span. */
  shortened: boolean;
}

/**
 * The instants an inclusive run of local days spans, as the API's half-open `[from, to)`, held to
 * the most recent 90 days the API accepts.
 */
export function rangeRequest(fromDay: string, toDay: string): RangeRequest {
  const to = startOfDay(nextDay(toDay));
  const from = startOfDay(fromDay);
  const earliest = new Date(to.getTime() - ANALYSIS_SPAN_DAYS * DAY_MS);
  return from < earliest
    ? { from: earliest, to, shortened: true }
    : { from, to, shortened: false };
}

/** The same number of local days ending where the range begins. */
export function previousPeriod(fromDay: string, dayCount: number): RangeRequest {
  const start = parseDate(fromDay);
  return rangeRequest(
    start.subtract({ days: dayCount }).toString(),
    start.subtract({ days: 1 }).toString()
  );
}

export interface DailyProfileRequest {
  query: DailyProfileQuery;
  primary: RangeRequest;
  comparison: RangeRequest | null;
  /** The viewer may read only the last 24 hours, so the range starts there and nothing is compared. */
  limitedTo24Hours: boolean;
}

export function dailyProfileRequest(
  filter: LoopAnalysisFilterState,
  range: { fromDay: string; toDay: string; dayCount: number },
  options: { limitTo24Hours: boolean; now: Date }
): DailyProfileRequest {
  const primary = rangeRequest(range.fromDay, range.toDay);
  let comparison: RangeRequest | null = null;

  if (options.limitTo24Hours) {
    const floor = new Date(options.now.getTime() - HISTORY_CLAMP_MS);
    if (primary.from < floor) primary.from = floor;
  } else if (filter.compare === "previous") {
    comparison = previousPeriod(range.fromDay, range.dayCount);
  } else if (filter.compare === "custom" && filter.compareFrom && filter.compareTo) {
    comparison =
      filter.compareFrom <= filter.compareTo
        ? rangeRequest(filter.compareFrom, filter.compareTo)
        : null;
  }

  const query: DailyProfileQuery = {
    from: primary.from.toISOString(),
    to: primary.to.toISOString(),
    excludeOverrides: filter.excludeOverrides,
    excludeTemporaryTargets: filter.excludeTemporaryTargets,
    excludeExercise: filter.excludeExercise,
    excludeIllness: filter.excludeIllness,
    excludeOpenLoop: filter.excludeOpenLoop,
  };
  if (comparison) {
    query.compareFrom = comparison.from.toISOString();
    query.compareTo = comparison.to.toISOString();
  }
  if (filter.weekdays.length > 0) query.days = filter.weekdays;
  if (filter.profileName) query.profileName = filter.profileName;

  return { query, primary, comparison, limitedTo24Hours: options.limitTo24Hours };
}
