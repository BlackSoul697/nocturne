import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { DayOfWeek } from "$lib/api";
import LoopAnalysisFilterBar from "./LoopAnalysisFilterBar.svelte";
import { DEFAULT_FILTER, rangeRequest } from "./loop-analysis-filter";

const primary = rangeRequest("2026-10-09", "2026-10-22");

describe("LoopAnalysisFilterBar", () => {
  it("shows the API's default exclusions and reports a weekday pick", async () => {
    const onchange = vi.fn();
    render(LoopAnalysisFilterBar, {
      props: { filter: DEFAULT_FILTER, onchange, primary, comparison: null, limitedTo24Hours: false, profileNames: ["Default"] },
    });

    await expect.element(page.getByLabelText("Illness")).toBeChecked();
    await expect.element(page.getByLabelText("Time not in closed loop")).toBeChecked();
    await expect.element(page.getByLabelText("Overrides")).not.toBeChecked();
    await expect.element(page.getByText("Every day")).toBeVisible();

    await page.getByLabelText("Exercise").click();
    expect(onchange).toHaveBeenLastCalledWith({ ...DEFAULT_FILTER, excludeExercise: true });

    const saturday = new Date(2026, 0, 10).toLocaleDateString(undefined, { weekday: "short" });
    await page.getByText(saturday, { exact: true }).click();
    expect(onchange).toHaveBeenLastCalledWith({ ...DEFAULT_FILTER, weekdays: [DayOfWeek.Saturday] });
  });

  it("explains a 24-hour-limited viewer's range and offers no comparison", async () => {
    render(LoopAnalysisFilterBar, {
      props: {
        filter: { ...DEFAULT_FILTER, compare: "previous" },
        onchange: vi.fn(),
        primary,
        comparison: null,
        limitedTo24Hours: true,
        profileNames: [],
      },
    });

    await expect.element(page.getByText(/You can see the last 24 hours only/)).toBeVisible();
    await expect.element(page.getByText("No comparison")).toBeVisible();
  });

  it("says why an incomplete or reversed custom comparison compares nothing", async () => {
    const props = {
      onchange: vi.fn(),
      primary,
      comparison: null,
      limitedTo24Hours: false,
      profileNames: [],
    };
    const { rerender } = render(LoopAnalysisFilterBar, {
      props: { ...props, filter: { ...DEFAULT_FILTER, compare: "custom", compareFrom: "2026-06-01" } },
    });
    await expect.element(page.getByText("Pick both dates to compare.")).toBeVisible();

    await rerender({
      ...props,
      filter: { ...DEFAULT_FILTER, compare: "custom", compareFrom: "2026-07-15", compareTo: "2026-06-01" },
    });
    await expect
      .element(page.getByText("The comparison has to end on or after the day it starts."))
      .toBeVisible();
  });

  it("says when a range was held to its most recent 90 days", async () => {
    render(LoopAnalysisFilterBar, {
      props: {
        filter: DEFAULT_FILTER,
        onchange: vi.fn(),
        primary: rangeRequest("2026-01-01", "2026-10-22"),
        comparison: null,
        limitedTo24Hours: false,
        profileNames: [],
      },
    });

    await expect.element(page.getByText(/covers at most 90 days/)).toBeVisible();
  });
});
