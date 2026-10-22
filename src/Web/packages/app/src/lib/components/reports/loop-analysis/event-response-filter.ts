import {
  EventAnchorKind,
  type EventCohortDefinition,
  type EventResponseDefinition,
  type EventUnavailableAnchor,
} from "$lib/api";
import type { getEventResponse } from "$api/generated/loopAnalysis.generated.remote";

type EventResponseQuery = Parameters<typeof getEventResponse>[0];

export const MAX_COHORTS = 3;
export const MINUTES_BEFORE = [0, 30, 60, 90, 120] as const;
export const MINUTES_AFTER = [120, 180, 240, 300, 360, 420, 480] as const;
export const ANCHOR_KINDS: EventAnchorKind[] = Object.values(EventAnchorKind);

/** Each cohort parameter, suffixed with the cohort's number as the API binds it. */
const COHORT_FIELDS = ["anchor", "fromMinute", "toMinute", "minGrams", "maxGrams"] as const;
/** The options' query parameters, as a response's unavailable filters name them. */
export const ISOLATED_ONLY = "isolatedOnly";
export const REQUIRE_BOLUS_CLUSTER = "requireBolusCluster";
const OPTION_PARAMS = [ISOLATED_ONLY, REQUIRE_BOLUS_CLUSTER] as const;
const WINDOW_PARAMS = ["minutesBefore", "minutesAfter"] as const;

/** Every query parameter the events builder owns; a write clears them all before setting its own. */
const EVENT_PARAM_NAMES = [
  ...[1, 2, 3].flatMap((n) => COHORT_FIELDS.map((field) => `${field}${n}`)),
  ...OPTION_PARAMS,
  ...WINDOW_PARAMS,
];

/**
 * The cohorts and window as the URL carries them, in the API's own definition and parameter names.
 * No cohorts means the API's default, which the response's `definition` then states; so does an
 * absent window.
 */
export type EventResponseState = EventResponseDefinition;

function toAnchor(value: string | null): EventAnchorKind | null {
  return ANCHOR_KINDS.find((kind) => kind === value) ?? null;
}

function toNumber(value: string | null): number | undefined {
  if (value == null || value === "") return undefined;
  const n = Number(value);
  return Number.isFinite(n) ? n : undefined;
}

export function readEventResponse(search: URLSearchParams): EventResponseState {
  const cohorts: EventCohortDefinition[] = [];
  for (let n = 1; n <= MAX_COHORTS; n++) {
    const anchor = toAnchor(search.get(`anchor${n}`));
    if (!anchor) continue;
    cohorts.push({
      cohort: cohorts.length + 1,
      anchor,
      fromMinute: toNumber(search.get(`fromMinute${n}`)),
      toMinute: toNumber(search.get(`toMinute${n}`)),
      minGrams: anchor === EventAnchorKind.Meal ? toNumber(search.get(`minGrams${n}`)) : undefined,
      maxGrams: anchor === EventAnchorKind.Meal ? toNumber(search.get(`maxGrams${n}`)) : undefined,
    });
  }
  const before = toNumber(search.get("minutesBefore"));
  const after = toNumber(search.get("minutesAfter"));
  return {
    cohorts,
    isolatedOnly: search.get("isolatedOnly") === "true",
    requireBolusCluster: search.get("requireBolusCluster") === "true",
    minutesBefore: MINUTES_BEFORE.find((m) => m === before),
    minutesAfter: MINUTES_AFTER.find((m) => m === after),
  };
}

/** The search params with the builder written into them, every other parameter left as it was. */
export function writeEventResponse(state: EventResponseState, search: URLSearchParams): URLSearchParams {
  const next = new URLSearchParams(search);
  for (const name of EVENT_PARAM_NAMES) next.delete(name);
  for (const [name, value] of Object.entries(eventParameters(state))) next.set(name, String(value));
  return next;
}

/** The builder's parameters, cohorts numbered in order; only what is set. */
function eventParameters(state: EventResponseState): Record<string, string | number | boolean> {
  const params: Record<string, string | number | boolean> = {};
  (state.cohorts ?? []).slice(0, MAX_COHORTS).forEach((cohort, i) => {
    if (!cohort.anchor) return;
    const n = i + 1;
    params[`anchor${n}`] = cohort.anchor;
    if (cohort.fromMinute != null) params[`fromMinute${n}`] = cohort.fromMinute;
    if (cohort.toMinute != null) params[`toMinute${n}`] = cohort.toMinute;
    if (cohort.anchor === EventAnchorKind.Meal) {
      if (cohort.minGrams != null) params[`minGrams${n}`] = cohort.minGrams;
      if (cohort.maxGrams != null) params[`maxGrams${n}`] = cohort.maxGrams;
    }
  });
  if (state.isolatedOnly) params.isolatedOnly = true;
  if (state.requireBolusCluster) params.requireBolusCluster = true;
  if (state.minutesBefore != null) params.minutesBefore = state.minutesBefore;
  if (state.minutesAfter != null) params.minutesAfter = state.minutesAfter;
  return params;
}

/** The event-response query: the shared filter's query with the builder's parameters. */
export function eventResponseQuery(
  base: Pick<EventResponseQuery, "startDate" | "endDate"> & Partial<EventResponseQuery>,
  state: EventResponseState
): EventResponseQuery {
  return { ...base, ...eventParameters(state) };
}

/**
 * The state without the cohorts whose anchor the caller cannot ask for and the options it cannot
 * use, or null when it holds none of them.
 */
export function withoutUnavailableEvents(
  state: EventResponseState,
  unavailableAnchors: readonly EventUnavailableAnchor[],
  unavailableFilters: readonly string[]
): EventResponseState | null {
  const anchors = new Set(unavailableAnchors.map((a) => a.anchor));
  const filters = new Set(unavailableFilters);
  const cohorts = (state.cohorts ?? []).filter((c) => !c.anchor || !anchors.has(c.anchor));
  const isolatedOnly = state.isolatedOnly && !filters.has(ISOLATED_ONLY);
  const requireBolusCluster = state.requireBolusCluster && !filters.has(REQUIRE_BOLUS_CLUSTER);
  if (
    cohorts.length === (state.cohorts ?? []).length &&
    isolatedOnly === state.isolatedOnly &&
    requireBolusCluster === state.requireBolusCluster
  )
    return null;
  return { ...state, cohorts, isolatedOnly, requireBolusCluster };
}

/** `HH:mm` for a minute of the day, as a time input reads it. */
export function minuteToClock(minute: number | null | undefined): string {
  if (minute == null) return "";
  const m = ((minute % 1440) + 1440) % 1440;
  return `${String(Math.floor(m / 60)).padStart(2, "0")}:${String(m % 60).padStart(2, "0")}`;
}

/** The minute of the day a time input's `HH:mm` names, or undefined when empty. */
export function clockToMinute(value: string): number | undefined {
  const match = /^(\d{1,2}):(\d{2})$/.exec(value);
  return match ? Number(match[1]) * 60 + Number(match[2]) : undefined;
}
