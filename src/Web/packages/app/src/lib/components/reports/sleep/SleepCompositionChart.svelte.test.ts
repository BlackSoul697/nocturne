import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-svelte";
import type { SleepNightSummary } from "$lib/api";
import SleepCompositionChart from "./SleepCompositionChart.svelte";

function night(displayDate: string): SleepNightSummary {
  return {
    sessionId: `session-${displayDate}`,
    displayDate,
    deepMinutes: 60,
    remMinutes: 90,
    lightMinutes: 240,
    awakeMinutes: 20,
    sleepMinutes: 390,
  };
}

function dayColumns(container: HTMLElement): SVGRectElement[] {
  return Array.from(container.querySelectorAll<SVGRectElement>("rect[fill='transparent']"));
}

describe("SleepCompositionChart", () => {
  it("draws one column per served day, gap days included", async () => {
    const days = ["2026-01-10", "2026-01-11", "2026-01-12"];

    const { container } = render(SleepCompositionChart, {
      nights: [night("2026-01-10"), night("2026-01-12")],
      days,
    });

    await expect.poll(() => dayColumns(container).length).toBe(days.length);
    const linked = dayColumns(container).map((column) => column.getAttribute("role") === "link");
    expect(linked).toEqual([true, false, true]);
  });

  it("drops a night whose display day is not in the served list", async () => {
    const { container } = render(SleepCompositionChart, {
      nights: [night("2026-01-09"), night("2026-01-10")],
      days: ["2026-01-10", "2026-01-11"],
    });

    await expect.poll(() => dayColumns(container).length).toBe(2);
    const linked = dayColumns(container).map((column) => column.getAttribute("role") === "link");
    expect(linked).toEqual([true, false]);
  });
});
