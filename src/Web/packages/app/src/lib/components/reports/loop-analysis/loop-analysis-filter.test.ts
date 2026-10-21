import { describe, expect, it } from "vitest";
import { DayOfWeek } from "$lib/api";
import {
  DEFAULT_FILTER,
  dailyProfileRequest,
  rangeRequest,
  readLoopAnalysisFilter,
  writeLoopAnalysisFilter,
  type LoopAnalysisFilterState,
} from "./loop-analysis-filter";

const DAY_MS = 86_400_000;
const HOUR_MS = 3_600_000;

const range14 = { fromDay: "2026-10-09", toDay: "2026-10-22", dayCount: 14 };
const now = new Date("2026-10-22T12:00:00Z");

function roundTrip(filter: LoopAnalysisFilterState, base = "") {
  return readLoopAnalysisFilter(writeLoopAnalysisFilter(filter, new URLSearchParams(base)));
}

describe("loop analysis filter URL", () => {
  it("reads a bare URL as the defaults, which exclude illness and open-loop time", () => {
    const filter = readLoopAnalysisFilter(new URLSearchParams());
    expect(filter).toEqual(DEFAULT_FILTER);
    expect(filter.excludeIllness).toBe(true);
    expect(filter.excludeOpenLoop).toBe(true);
    expect(filter.excludeOverrides).toBe(false);
  });

  it("writes nothing for the defaults", () => {
    expect(writeLoopAnalysisFilter(DEFAULT_FILTER, new URLSearchParams()).toString()).toBe("");
  });

  it("round-trips every filter", () => {
    const filter: LoopAnalysisFilterState = {
      tab: "daily-profile",
      compare: "custom",
      compareFrom: "2026-06-01",
      compareTo: "2026-07-15",
      weekdays: [DayOfWeek.Sunday, DayOfWeek.Saturday],
      profileName: "Weekend & travel",
      excludeOverrides: true,
      excludeTemporaryTargets: true,
      excludeExercise: true,
      excludeIllness: false,
      excludeOpenLoop: false,
    };
    expect(roundTrip(filter)).toEqual({ ...filter, weekdays: [DayOfWeek.Sunday, DayOfWeek.Saturday] });
  });

  it("round-trips the previous-period comparison without custom dates", () => {
    const filter = { ...DEFAULT_FILTER, compare: "previous" as const, compareFrom: "2026-01-01", compareTo: "2026-01-05" };
    const search = writeLoopAnalysisFilter(filter, new URLSearchParams());
    expect(search.has("compareFrom")).toBe(false);
    expect(readLoopAnalysisFilter(search)).toEqual({ ...filter, compareFrom: null, compareTo: null });
  });

  it("keeps the shared report range and other parameters it does not own", () => {
    const search = writeLoopAnalysisFilter(
      { ...DEFAULT_FILTER, weekdays: [DayOfWeek.Monday] },
      new URLSearchParams("days=30&from=2026-09-23&to=2026-10-22&isDefault=false&weekdays=5")
    );
    expect(search.get("days")).toBe("30");
    expect(search.get("from")).toBe("2026-09-23");
    expect(search.get("isDefault")).toBe("false");
    expect(search.getAll("weekdays")).toEqual(["1"]);
  });

  it("drops malformed values rather than passing them to the API", () => {
    const filter = readLoopAnalysisFilter(
      new URLSearchParams("tab=nope&compare=sideways&compareFrom=yesterday&weekdays=1,9,x,1&excludeIllness=maybe")
    );
    expect(filter.tab).toBe("daily-profile");
    expect(filter.compare).toBe("none");
    expect(filter.compareFrom).toBeNull();
    expect(filter.weekdays).toEqual([DayOfWeek.Monday]);
    expect(filter.excludeIllness).toBe(true);
  });
});

describe("daily profile request", () => {
  it("spans the inclusive local days as a half-open range", () => {
    const { primary, comparison, query } = dailyProfileRequest(DEFAULT_FILTER, range14, {
      limitTo24Hours: false,
      now,
    });
    const span = primary.to.getTime() - primary.from.getTime();
    expect(Math.abs(span - 14 * DAY_MS)).toBeLessThanOrEqual(HOUR_MS);
    expect(comparison).toBeNull();
    expect(query.compareFrom).toBeUndefined();
    expect(query.days).toBeUndefined();
    expect(query.excludeIllness).toBe(true);
  });

  it("compares with the same number of days ending where the range begins", () => {
    const { primary, comparison, query } = dailyProfileRequest(
      { ...DEFAULT_FILTER, compare: "previous" },
      range14,
      { limitTo24Hours: false, now }
    );
    expect(comparison?.to.getTime()).toBe(primary.from.getTime());
    const span = comparison!.to.getTime() - comparison!.from.getTime();
    expect(Math.abs(span - 14 * DAY_MS)).toBeLessThanOrEqual(HOUR_MS);
    expect(query.compareTo).toBe(primary.from.toISOString());
  });

  it("ignores a custom comparison that runs backwards", () => {
    const { comparison } = dailyProfileRequest(
      { ...DEFAULT_FILTER, compare: "custom", compareFrom: "2026-07-15", compareTo: "2026-06-01" },
      range14,
      { limitTo24Hours: false, now }
    );
    expect(comparison).toBeNull();
  });

  it("starts a 24-hour-limited viewer's range 24 hours ago and compares nothing", () => {
    const { primary, comparison, query, limitedTo24Hours } = dailyProfileRequest(
      { ...DEFAULT_FILTER, compare: "previous" },
      range14,
      { limitTo24Hours: true, now }
    );
    expect(limitedTo24Hours).toBe(true);
    expect(primary.from.getTime()).toBe(now.getTime() - DAY_MS);
    expect(comparison).toBeNull();
    expect(query.compareFrom).toBeUndefined();
  });

  it("passes weekdays and profile through", () => {
    const { query } = dailyProfileRequest(
      { ...DEFAULT_FILTER, weekdays: [DayOfWeek.Saturday], profileName: "Default" },
      range14,
      { limitTo24Hours: false, now }
    );
    expect(query.days).toEqual([DayOfWeek.Saturday]);
    expect(query.profileName).toBe("Default");
  });

  it("holds a longer range to its most recent 90 days", () => {
    const longer = rangeRequest("2026-01-01", "2026-10-22");
    expect(longer.shortened).toBe(true);
    expect(longer.to.getTime() - longer.from.getTime()).toBe(90 * DAY_MS);
    expect(rangeRequest("2026-07-25", "2026-10-22").shortened).toBe(false);
  });
});
