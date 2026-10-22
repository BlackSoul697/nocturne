import { describe, expect, it } from "vitest";
import { EventAnchorKind } from "$lib/api";
import {
  clockToMinute,
  eventResponseQuery,
  minuteToClock,
  readEventResponse,
  withoutUnavailableEvents,
  writeEventResponse,
} from "./event-response-filter";
import { sortEvents } from "./event-response-view";

describe("event response filter", () => {
  it("round-trips the cohorts and window through the URL, keeping the parameters it does not own", () => {
    const search = new URLSearchParams(
      "days=14&tab=meals-events&anchor1=Meal&fromMinute1=300&toMinute1=600&minGrams1=40&anchor3=UnannouncedRise&isolatedOnly=true&minutesAfter=480"
    );

    const state = readEventResponse(search);
    expect(state.cohorts).toEqual([
      { cohort: 1, anchor: EventAnchorKind.Meal, fromMinute: 300, toMinute: 600, minGrams: 40, maxGrams: undefined },
      { cohort: 2, anchor: EventAnchorKind.UnannouncedRise, fromMinute: undefined, toMinute: undefined, minGrams: undefined, maxGrams: undefined },
    ]);
    expect(state.isolatedOnly).toBe(true);
    expect(state.minutesAfter).toBe(480);
    expect(state.minutesBefore).toBeUndefined();

    const written = writeEventResponse(state, search);
    expect(written.get("days")).toBe("14");
    expect(written.get("tab")).toBe("meals-events");
    expect(written.get("anchor2")).toBe("UnannouncedRise");
    expect(written.has("anchor3")).toBe(false);
    expect(readEventResponse(written)).toEqual(state);
  });

  it("reads no cohorts as the API's default, and drops what it cannot read", () => {
    const state = readEventResponse(new URLSearchParams("anchor1=Lunch&minutesAfter=7&minGrams2=20"));
    expect(state.cohorts).toEqual([]);
    expect(state.minutesAfter).toBeUndefined();
    expect(eventResponseQuery({ startDate: "2026-10-01", endDate: "2026-10-14" }, state)).toEqual({
      startDate: "2026-10-01",
      endDate: "2026-10-14",
    });
  });

  it("sends grams only for a meal cohort", () => {
    const query = eventResponseQuery(
      { startDate: "2026-10-01", endDate: "2026-10-14", excludeIllness: false },
      {
        cohorts: [
          { anchor: EventAnchorKind.CorrectionBolus, minGrams: 20, fromMinute: 0, toMinute: 120 },
          { anchor: EventAnchorKind.Meal, maxGrams: 50 },
        ],
        requireBolusCluster: true,
      }
    );
    expect(query).toEqual({
      startDate: "2026-10-01",
      endDate: "2026-10-14",
      excludeIllness: false,
      anchor1: EventAnchorKind.CorrectionBolus,
      fromMinute1: 0,
      toMinute1: 120,
      anchor2: EventAnchorKind.Meal,
      maxGrams2: 50,
      requireBolusCluster: true,
    });
  });

  it("drops the cohorts and options the viewer cannot use, or reports nothing to drop", () => {
    const state = readEventResponse(new URLSearchParams("anchor1=Meal&anchor2=SensorStart&isolatedOnly=true"));

    const next = withoutUnavailableEvents(
      state,
      [{ anchor: EventAnchorKind.Meal, requiredScope: "treatments.read" }],
      ["isolatedOnly", "requireBolusCluster"]
    );
    expect(next?.cohorts?.map((c) => c.anchor)).toEqual([EventAnchorKind.SensorStart]);
    expect(next?.isolatedOnly).toBe(false);
    expect(withoutUnavailableEvents(next!, [{ anchor: EventAnchorKind.Meal }], ["isolatedOnly"])).toBeNull();
  });

  it("converts minutes of the day to and from a time input", () => {
    expect(minuteToClock(1435)).toBe("23:55");
    expect(minuteToClock(undefined)).toBe("");
    expect(clockToMinute("07:30")).toBe(450);
    expect(clockToMinute("")).toBeUndefined();
  });

  it("sorts events with the figure missing last either way", () => {
    const events = [
      { at: "2026-10-01T00:00:00Z", metrics: { peakRise: 10 } },
      { at: "2026-10-02T00:00:00Z", metrics: {} },
      { at: "2026-10-03T00:00:00Z", metrics: { peakRise: 30 } },
    ];
    expect(sortEvents(events, "peakRise", true).map((e) => e.at?.slice(8, 10))).toEqual(["03", "01", "02"]);
    expect(sortEvents(events, "peakRise", false).map((e) => e.at?.slice(8, 10))).toEqual(["01", "03", "02"]);
  });
});
