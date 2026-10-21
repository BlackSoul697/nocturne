import { render } from "vitest-browser-svelte";
import { page, userEvent } from "vitest/browser";
import { describe, expect, it } from "vitest";
import { TimeZoneUnavailableReason } from "$lib/api";
import DailyProfileTab from "./DailyProfileTab.svelte";
import { profileResponse } from "./daily-profile.test-fixtures";

const insulinStats = { tdd: 30.2, microBolusInsulin: 120.5, microBolusCount: 640 };
const ratios = { averageBasalPercent: 48, averageBolusPercent: 52 };

async function hoverSlot(panel: string, steps: number) {
  const slider = page.getByRole("slider", { name: panel });
  (slider.element() as HTMLElement).focus();
  for (let i = 0; i < steps; i++) await userEvent.keyboard("{Shift>}{ArrowRight}{/Shift}");
  return page.getByRole("tooltip");
}

describe("DailyProfileTab", () => {
  it("lays every series out on the shared time-of-day axis with the summary strip", async () => {
    render(DailyProfileTab, { props: { response: profileResponse(), insulinStats, ratios } });

    for (const title of [
      "Glucose",
      "Basal",
      "Net loop correction",
      "Net loop correction by hour",
      "Insulin on board",
      "Carbs on board",
      "Manual boluses",
      "Carbs",
    ])
      await expect.element(page.getByRole("slider", { name: title, exact: true })).toBeVisible();

    await expect.element(page.getByText("Days with data")).toBeVisible();
    await expect.element(page.getByText("30.2")).toBeVisible();
    await expect.element(page.getByText("48% / 52%")).toBeVisible();
    await expect.element(page.getByText("0.88×")).toBeVisible();
    await expect.element(page.getByText("+9.60")).toBeVisible();
    await expect.element(page.getByText("32% of total insulin over 14 days")).toBeVisible();
  });

  it("shows the slot's percentiles and n in the tooltip", async () => {
    render(DailyProfileTab, { props: { response: profileResponse() } });

    const tooltip = await hoverSlot("Insulin on board", 1);
    await expect.element(tooltip).toBeVisible();
    await expect.element(tooltip.getByText("P90")).toBeVisible();
    await expect.element(tooltip.getByText("2.50")).toBeVisible();
    await expect.element(tooltip.getByText("14 days")).toBeVisible();
    await expect.element(tooltip.getByText("Mostly as the loop reported it")).toBeVisible();
    expect(tooltip.getByText("Too few days to read as a pattern").elements()).toHaveLength(0);
  });

  it("marks sparse slots as too thin to read as a pattern", async () => {
    render(DailyProfileTab, { props: { response: profileResponse({ sparse: true }) } });

    const tooltip = await hoverSlot("Glucose", 1);
    await expect.element(tooltip.getByText("Too few days to read as a pattern")).toBeVisible();
    await expect.element(page.getByText(/Faded stretches rest on too few days/)).toBeVisible();
  });

  it("draws the comparison and reads it beside the primary range", async () => {
    render(DailyProfileTab, { props: { response: profileResponse({ comparison: true }) } });

    await expect
      .element(page.getByText("The comparison range is drawn muted and dashed under each series."))
      .toBeVisible();
    const tooltip = await hoverSlot("Insulin on board", 2);
    await expect.element(tooltip.getByText("Compared")).toBeVisible();
    await expect.element(tooltip.getByRole("row", { name: "Median 1.50 0.75" })).toBeVisible();
  });

  it("lists the days behind a picked slot, each linking to Day in Review at that time", async () => {
    render(DailyProfileTab, { props: { response: profileResponse({ comparison: true }) } });

    await hoverSlot("Glucose", 8);
    await userEvent.keyboard("{Enter}");

    await expect.element(page.getByRole("heading", { name: /^Days behind/ })).toBeVisible();
    await expect.element(page.getByRole("heading", { name: "Comparison range" })).toBeVisible();
    const link = page.getByRole("link").first();
    await expect.element(link).toHaveAttribute("href", "/reports/day-in-review?date=2026-10-01&time=08:00");
  });

  it("says treatment series are hidden from a caller without treatments access", async () => {
    render(DailyProfileTab, {
      props: { response: profileResponse({ withholdTreatments: true, withholdSchedule: true }) },
    });

    expect(page.getByText("Hidden: you do not have access to treatments").elements()).toHaveLength(4);
    expect(page.getByRole("slider", { name: "Insulin on board" }).elements()).toHaveLength(0);
    await expect.element(page.getByRole("slider", { name: "Glucose" })).toBeVisible();
    expect(page.getByText("Scheduled", { exact: true }).elements()).toHaveLength(0);
  });

  it("explains a missing time zone instead of drawing anything", async () => {
    render(DailyProfileTab, {
      props: {
        response: {
          primary: { slots: [], days: [] },
          timeZoneUnavailableReason: TimeZoneUnavailableReason.NotConfigured,
          historyClamped: false,
          ignoredFilters: [],
        },
      },
    });

    await expect.element(page.getByText("This report needs a time zone")).toBeVisible();
    await expect.element(page.getByText(/No time zone is set in the profile settings/)).toBeVisible();
    expect(page.getByRole("slider").elements()).toHaveLength(0);
  });

  it("says why a shared link has no time-of-day view", async () => {
    render(DailyProfileTab, {
      props: {
        response: {
          primary: { slots: [], days: [] },
          timeZoneUnavailableReason: TimeZoneUnavailableReason.Share,
        },
      },
    });

    await expect.element(page.getByText(/A shared link cannot see that time zone/)).toBeVisible();
  });

  it("says the ranges were clamped to the last 24 hours", async () => {
    render(DailyProfileTab, { props: { response: profileResponse({ historyClamped: true }) } });

    await expect.element(page.getByText("Only the last 24 hours are included")).toBeVisible();
  });

  it("names the filters the caller's access made the API ignore", async () => {
    render(DailyProfileTab, {
      props: { response: { ...profileResponse(), ignoredFilters: ["profileName", "excludeOpenLoop"] } },
    });

    await expect.element(page.getByText("Some filters were not applied")).toBeVisible();
    await expect.element(page.getByText(/Profile, Time not in closed loop\./)).toBeVisible();
  });
});
