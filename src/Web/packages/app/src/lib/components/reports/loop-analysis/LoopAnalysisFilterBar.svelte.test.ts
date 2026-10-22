import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { DayOfWeek, LoopAnalysisComparison } from "$lib/api";
import LoopAnalysisFilterBar from "./LoopAnalysisFilterBar.svelte";
import { readLoopAnalysisFilter } from "./loop-analysis-filter";
import { unavailableFor } from "./daily-profile.test-fixtures";

const bare = () => readLoopAnalysisFilter(new URLSearchParams());

const base = {
  onchange: vi.fn(),
  requested: { startDate: "2026-10-09", endDate: "2026-10-22" },
  settled: true,
  clamped: false,
  unavailable: [],
  profileNames: ["Default"],
};

const applied = {
  days: [0, 1, 2, 3, 4, 5, 6],
  excludeOverrides: false,
  excludeTemporaryTargets: false,
  excludeExercise: false,
  excludeIllness: true,
  excludeOpenLoop: true,
};

describe("LoopAnalysisFilterBar", () => {
  it("shows the filters the API applied and reports a change as only what the URL sets", async () => {
    const onchange = vi.fn();
    render(LoopAnalysisFilterBar, { props: { ...base, onchange, filter: bare(), applied } });

    await expect.element(page.getByLabelText("Illness")).toBeChecked();
    await expect.element(page.getByLabelText("Time not in closed loop")).toBeChecked();
    await expect.element(page.getByLabelText("Overrides")).not.toBeChecked();
    await expect.element(page.getByText("Every day")).toBeVisible();

    await page.getByLabelText("Exercise").click();
    expect(onchange).toHaveBeenLastCalledWith({ ...bare(), masks: { excludeExercise: true } });

    const saturday = new Date(2026, 0, 10).toLocaleDateString(undefined, { weekday: "short" });
    await page.getByText(saturday, { exact: true }).click();
    expect(onchange).toHaveBeenLastCalledWith({ ...bare(), weekdays: [DayOfWeek.Saturday] });
  });

  it("disables the filters the caller's access cannot use, and says why", async () => {
    render(LoopAnalysisFilterBar, {
      props: {
        ...base,
        filter: bare(),
        applied: { ...applied, excludeOpenLoop: false },
        unavailable: unavailableFor(["glucose.read", "treatments.read", "reports.read"]),
      },
    });

    const openLoop = page.getByLabelText("Time not in closed loop");
    await expect.element(openLoop).toBeDisabled();
    await expect.element(openLoop).not.toBeChecked();
    await expect.element(page.getByText("(Needs access to devices)")).toBeVisible();
    await expect.element(page.getByText("Needs access to therapy settings")).toBeVisible();
    await expect.element(page.getByLabelText("Illness")).toBeEnabled();
  });

  it("shows the range the API resolved and says when it was trimmed", async () => {
    render(LoopAnalysisFilterBar, {
      props: {
        ...base,
        filter: { ...bare(), compare: LoopAnalysisComparison.Previous },
        applied,
        primaryRange: { startDate: "2026-07-25", endDate: "2026-10-22", shortened: true },
        comparisonRange: { startDate: "2026-04-26", endDate: "2026-07-24", shortened: false },
      },
    });

    await expect.element(page.getByText("Jul 25 – Oct 22, 2026")).toBeVisible();
    await expect.element(page.getByText("Apr 26 – Jul 24, 2026")).toBeVisible();
    await expect.element(page.getByText(/covers at most 90 days/)).toBeVisible();
  });

  it("says when only the comparison, a previous period, was trimmed", async () => {
    render(LoopAnalysisFilterBar, {
      props: {
        ...base,
        filter: { ...bare(), compare: LoopAnalysisComparison.Previous },
        primaryRange: { startDate: "2026-10-09", endDate: "2026-10-22", shortened: false },
        comparisonRange: { startDate: "2026-09-25", endDate: "2026-10-08", shortened: true },
      },
    });

    await expect.element(page.getByText(/covers at most 90 days/)).toBeVisible();
  });

  it("ignores the picked range for a clamped viewer, compares nothing and says why", async () => {
    render(LoopAnalysisFilterBar, {
      props: {
        ...base,
        filter: { ...bare(), compare: LoopAnalysisComparison.Previous },
        clamped: true,
        requested: { startDate: "2026-01-01", endDate: "2026-01-14" },
        primaryRange: {
          startDate: "2026-10-21",
          endDate: "2026-10-22",
          from: "2026-10-21T10:00:00Z",
          to: "2026-10-22T10:00:00Z",
          shortened: false,
        },
      },
    });

    await expect.element(page.getByText(/You can see the last 24 hours only/)).toBeVisible();
    await expect.element(page.getByText("Last 24 hours, Oct 21 – 22, 2026")).toBeVisible();
    await expect.element(page.getByText("No comparison")).toBeVisible();
    expect(page.getByText(/Jan 1/).elements()).toHaveLength(0);
    expect(page.getByText(/covers at most 90 days/).elements()).toHaveLength(0);
  });

  it("shows an echo the API has not answered yet as loading, not as the last answer", async () => {
    render(LoopAnalysisFilterBar, {
      props: {
        ...base,
        filter: bare(),
        settled: false,
        applied: { ...applied, profileName: "Weekend" },
        primaryRange: { startDate: "2026-07-25", endDate: "2026-10-22", shortened: true },
      },
    });

    const illness = page.getByLabelText("Illness");
    await expect.element(illness).not.toBeChecked();
    await expect.element(illness).toBeDisabled();
    expect(illness.element().getAttribute("aria-checked")).not.toBe("mixed");
    await expect.element(page.getByRole("group", { name: "Exclude" })).toHaveAttribute("aria-busy", "true");
    await expect.element(page.getByText("Loading the exclusions the report applied")).toBeInTheDocument();
    await expect.element(page.getByText("All profiles")).toBeVisible();
    await expect.element(page.getByText("Oct 9 – 22, 2026")).toBeVisible();
    expect(page.getByText(/covers at most 90 days/).elements()).toHaveLength(0);
  });

  it("says why an incomplete or reversed custom comparison compares nothing", async () => {
    const custom = { ...bare(), compare: LoopAnalysisComparison.Custom };
    const { rerender } = render(LoopAnalysisFilterBar, {
      props: { ...base, filter: { ...custom, compareFrom: "2026-06-01" } },
    });
    await expect.element(page.getByText("Pick both dates to compare.")).toBeVisible();

    await rerender({ ...base, filter: { ...custom, compareFrom: "2026-07-15", compareTo: "2026-06-01" } });
    await expect
      .element(page.getByText("The comparison has to end on or after the day it starts."))
      .toBeVisible();
  });
});
