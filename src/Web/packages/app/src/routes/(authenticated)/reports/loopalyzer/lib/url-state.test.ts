import { describe, expect, it } from "vitest";
import {
  decodeEventTypes,
  decodeTimeWindow,
  encodeEventTypes,
  encodeTimeWindow,
} from "./url-state";

describe("decodeTimeWindow", () => {
  it("decodes valid HH:MM-HH:MM", () => {
    expect(decodeTimeWindow("06:00-14:30")).toEqual({ startMin: 360, endMin: 870 });
  });

  it("returns default for null/empty", () => {
    expect(decodeTimeWindow(null)).toEqual({ startMin: 360, endMin: 1200 });
    expect(decodeTimeWindow("")).toEqual({ startMin: 360, endMin: 1200 });
  });

  it("returns default for malformed input", () => {
    expect(decodeTimeWindow("not-a-window")).toEqual({ startMin: 360, endMin: 1200 });
  });

  it("returns default when end <= start", () => {
    expect(decodeTimeWindow("12:00-08:00")).toEqual({ startMin: 360, endMin: 1200 });
  });

  it("clamps out-of-range minutes", () => {
    expect(decodeTimeWindow("25:00-26:00")).toEqual({ startMin: 360, endMin: 1200 });
  });
});

describe("encodeTimeWindow round-trip", () => {
  it("preserves canonical inputs", () => {
    const cases = [
      { startMin: 0, endMin: 60 },
      { startMin: 360, endMin: 870 },
      { startMin: 23 * 60, endMin: 24 * 60 },
    ];
    for (const c of cases) {
      expect(decodeTimeWindow(encodeTimeWindow(c))).toEqual(c);
    }
  });
});

describe("event types codec", () => {
  it("decodes empty/null to empty array", () => {
    expect(decodeEventTypes(null)).toEqual([]);
    expect(decodeEventTypes("")).toEqual([]);
  });

  it("decodes csv with whitespace trimming", () => {
    expect(decodeEventTypes(" Meal, Snack ,Correction Bolus")).toEqual([
      "Meal",
      "Snack",
      "Correction Bolus",
    ]);
  });

  it("encodes and round-trips", () => {
    const types = ["Meal Bolus", "Snack"];
    expect(decodeEventTypes(encodeEventTypes(types))).toEqual(types);
  });

  it("filters empty strings on encode", () => {
    expect(encodeEventTypes(["A", "", "B"])).toBe("A,B");
  });
});
