import { describe, expect, it } from "vitest";
import { dayInReviewSearch, gapRows, hourOfSlot, shareBars } from "./loop-health-view";

describe("loop-health-view", () => {
  it("draws each measured hour over its twelve slots, keeping a zero and skipping an unmeasured hour", () => {
    const bars = shareBars({
      hours: [
        { hour: 0, percent: 0, daily: { n: 14, sparse: false } },
        { hour: 1 },
        { hour: 23, percent: 50, daily: { n: 2, sparse: true } },
      ],
    });

    expect(bars).toEqual([
      { start: 0, end: 12, value: 0, sparse: false },
      { start: 276, end: 288, value: 50, sparse: true },
    ]);
  });

  it("puts both kinds of gap in one list, longest first", () => {
    const rows = gapRows(
      { gaps: [{ date: "2026-10-03", startMinuteOfDay: 1410, minutes: 90 }] },
      { gaps: [{ date: "2026-10-08", startMinuteOfDay: 360, minutes: 300 }] }
    );

    expect(rows.map((r) => [r.kind, r.gap.minutes])).toEqual([
      ["unrecorded", 300],
      ["loop", 90],
    ]);
  });

  it("opens Day in Review on the local day and time a gap began", () => {
    expect(dayInReviewSearch({ date: "2026-10-03", startMinuteOfDay: 1410 })).toBe("date=2026-10-03&time=23:30");
  });

  it("reads the hour a slot falls in", () => {
    expect(hourOfSlot(0)).toBe(0);
    expect(hourOfSlot(287)).toBe(23);
  });
});
