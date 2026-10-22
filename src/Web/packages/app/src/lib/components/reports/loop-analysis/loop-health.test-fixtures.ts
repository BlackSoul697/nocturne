import {
  DayOfWeek,
  LoopAnalysisSeries,
  type LoopAnalysisAggregate,
  type LoopHealthDistribution,
  type LoopHealthGaps,
  type LoopHealthRange,
  type LoopHealthResponse,
  type LoopHealthShare,
} from "$lib/api";
import { ALL_SCOPES, unavailableFor } from "./daily-profile.test-fixtures";

const HOURS = Array.from({ length: 24 }, (_, hour) => hour);

function aggregate(centre: number, spread: number, n: number, sparse: boolean): LoopAnalysisAggregate {
  return { p10: centre - 2 * spread, p25: centre - spread, p50: centre, p75: centre + spread, p90: centre + 2 * spread, mean: centre, n, sparse };
}

function share(percent: number | null, n: number, sparse: boolean): LoopHealthShare {
  return {
    percent: percent ?? undefined,
    daily: percent == null ? undefined : aggregate(percent, 2, n, sparse),
    hours: HOURS.map((hour) => ({
      hour,
      percent: percent == null ? undefined : percent + (hour === 3 ? 5 : 0),
      daily: percent == null ? undefined : aggregate(percent, 2, n, sparse),
    })),
  };
}

function distribution(centre: number, n: number, sparse: boolean): LoopHealthDistribution {
  return {
    overall: aggregate(centre, 0.1, n, sparse),
    hours: HOURS.map((hour) => ({ hour, value: aggregate(centre, 0.1, n, sparse) })),
  };
}

function gaps(list: { date: string; startMinuteOfDay: number; minutes: number }[]): LoopHealthGaps {
  return {
    count: list.length,
    totalMinutes: list.reduce((sum, g) => sum + g.minutes, 0),
    longestMinutes: list.length > 0 ? Math.max(...list.map((g) => g.minutes)) : undefined,
    countByStartHour: HOURS.map((hour) => list.filter((g) => Math.floor(g.startMinuteOfDay / 60) === hour).length),
    time: share(list.length > 0 ? 3.5 : 0, 14, false),
    gaps: list.map((g) => ({ ...g, start: `${g.date}T00:00:00Z`, end: `${g.date}T01:00:00Z` })),
  };
}

export interface LoopHealthOptions {
  /** Loop records no suggestion and no max IOB, and keeps its max basal rate as a setting. */
  system?: "oref" | "loop";
  /** Scales the shares, so a comparison range is told apart from the primary. */
  scale?: number;
  /** No temp's cap is known. */
  capUnknown?: boolean;
  days?: number;
  sparse?: boolean;
}

export function loopHealthRange(options: LoopHealthOptions = {}): LoopHealthRange {
  const { system = "oref", scale = 1, capUnknown = false, days = 14, sparse = false } = options;
  const oref = system === "oref";
  const n = sparse ? 2 : days;
  return {
    from: "2026-10-01T00:00:00Z",
    to: "2026-10-15T00:00:00Z",
    days: Array.from({ length: n }, (_, i) => ({
      date: `2026-10-${String(i + 1).padStart(2, "0")}`,
      dayOfWeek: DayOfWeek.Monday,
      start: `2026-10-${String(i + 1).padStart(2, "0")}T00:00:00Z`,
    })),
    activity: {
      aidActivePercent: 97.5 * scale,
      pumpUsePercent: 98 * scale,
      cgmActivePercent: 99.2 * scale,
      closedLoop: share(91.1 * scale, n, sparse),
      pump: share(92.4 * scale, n, sparse),
      cgm: share(98.7 * scale, n, sparse),
    },
    cadence: {
      loopGaps: gaps([
        { date: "2026-10-03", startMinuteOfDay: 23 * 60 + 30, minutes: 90 },
        { date: "2026-10-05", startMinuteOfDay: 3 * 60 + 10, minutes: 25 },
      ]),
      unrecordedDelivery: gaps([{ date: "2026-10-08", startMinuteOfDay: 6 * 60, minutes: 300 }]),
    },
    zeroDelivery: {
      algorithmZeroTemp: share(41 * scale, n, sparse),
      manualZeroTemp: share(0, n, sparse),
      suspended: share(1.2, n, sparse),
    },
    maxBasal: capUnknown
      ? { atCap: share(null, n, sparse), capKnown: share(0, n, sparse) }
      : { atCap: share(2.5 * scale, n, sparse), capKnown: share(76.7, n, sparse) },
    atMaxIob: oref ? share(0.1 * scale, n, sparse) : undefined,
    automaticBoluses: {
      perDay: distribution(54 * scale, n, sparse),
      unitsPerDay: distribution(12.3, n, sparse),
      size: distribution(0.2, n, sparse),
    },
    suggestedNotEnacted: oref ? share(0.2 * scale, n, sparse) : undefined,
    sensitivityRatio: oref ? distribution(1.1, n, sparse) : { hours: HOURS.map((hour) => ({ hour })) },
    limits: oref ? { orefMaxIob: 15 } : { loopMaximumBasalRate: 3 },
  };
}

const S = LoopAnalysisSeries;

/** The series `LoopAnalysisReadScopeGuard.LoopHealthWithheld` withholds for these scopes. */
export function loopHealthWithheldFor(scopes: readonly string[]): LoopAnalysisSeries[] {
  const glucose = scopes.includes("glucose.read");
  const treatments = scopes.includes("treatments.read");
  const devices = scopes.includes("devices.read");
  const therapy = scopes.includes("therapy.read");
  return [
    ...(glucose ? [] : [S.CgmActivity]),
    ...(treatments && devices ? [] : [S.LoopActivity, S.UnrecordedDelivery]),
    ...(treatments ? [] : [S.ZeroDelivery, S.AutomaticBoluses]),
    ...(devices && treatments && therapy ? [] : [S.MaxBasal]),
    ...(devices ? [] : [S.LoopGaps, S.AtMaxIob, S.SuggestedNotEnacted, S.SensitivityRatio]),
    ...(therapy ? [] : [S.LoopLimits]),
    ...(therapy && devices ? [] : [S.OrefMaxIob]),
  ];
}

/** Empties exactly what `LoopAnalysisReadScopeGuard.Narrow` empties for these series. */
function narrow(range: LoopHealthRange, withheld: Set<LoopAnalysisSeries>): LoopHealthRange {
  const hide = (series: LoopAnalysisSeries) => withheld.has(series);
  const activity = range.activity ?? {};
  const cadence = range.cadence ?? {};
  const limits = range.limits ?? {};
  if (hide(S.CgmActivity)) {
    activity.cgm = undefined;
    activity.cgmActivePercent = undefined;
  }
  if (hide(S.LoopActivity)) {
    activity.closedLoop = undefined;
    activity.pump = undefined;
    activity.aidActivePercent = undefined;
    activity.pumpUsePercent = undefined;
  }
  if (hide(S.LoopGaps)) cadence.loopGaps = undefined;
  if (hide(S.UnrecordedDelivery)) cadence.unrecordedDelivery = undefined;
  if (hide(S.ZeroDelivery)) range.zeroDelivery = undefined;
  if (hide(S.MaxBasal)) range.maxBasal = undefined;
  if (hide(S.AtMaxIob)) range.atMaxIob = undefined;
  if (hide(S.AutomaticBoluses)) range.automaticBoluses = undefined;
  if (hide(S.SuggestedNotEnacted)) range.suggestedNotEnacted = undefined;
  if (hide(S.SensitivityRatio)) range.sensitivityRatio = undefined;
  if (hide(S.LoopLimits)) limits.loopMaximumBasalRate = undefined;
  if (hide(S.OrefMaxIob)) limits.orefMaxIob = undefined;
  return range;
}

export function loopHealthResponse(
  options: LoopHealthOptions & { comparison?: boolean; historyClamped?: boolean; scopes?: readonly string[] } = {}
): LoopHealthResponse {
  const scopes = options.scopes ?? ALL_SCOPES;
  const withheld = loopHealthWithheldFor(scopes);
  const set = new Set(withheld);
  return {
    primary: narrow(loopHealthRange(options), set),
    comparison: options.comparison ? narrow(loopHealthRange({ ...options, scale: 0.5 }), set) : undefined,
    primaryRange: { startDate: "2026-10-01", endDate: "2026-10-14", from: "2026-10-01T00:00:00Z", to: "2026-10-15T00:00:00Z", shortened: false },
    comparisonRange: options.comparison
      ? { startDate: "2026-09-17", endDate: "2026-09-30", from: "2026-09-17T00:00:00Z", to: "2026-10-01T00:00:00Z", shortened: false }
      : undefined,
    appliedFilters: {
      days: [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday],
      excludeOverrides: false,
      excludeTemporaryTargets: false,
      excludeExercise: false,
      excludeIllness: true,
      excludeOpenLoop: true,
    },
    historyClamped: options.historyClamped ?? false,
    ignoredFilters: [],
    withheldSeries: withheld,
    unavailableFilters: unavailableFor(scopes),
  };
}
