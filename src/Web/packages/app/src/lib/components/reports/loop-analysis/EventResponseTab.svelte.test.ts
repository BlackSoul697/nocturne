import { render } from "vitest-browser-svelte";
import { page, userEvent } from "vitest/browser";
import { describe, expect, it } from "vitest";
import { LoopAnalysisSeries, TimeZoneUnavailableReason } from "$lib/api";
import EventResponseTab from "./EventResponseTab.svelte";
import { eventResponse, TREATMENT_SERIES } from "./event-response.test-fixtures";

const PANELS = [
  "Glucose change from the event",
  "Glucose",
  "Net loop correction",
  "Net insulin above schedule since the event",
  "Insulin on board",
  "Carbs on board",
];

describe("EventResponseTab", () => {
  it("stacks every series on the shared axis of minutes from the event", async () => {
    render(EventResponseTab, { props: { response: eventResponse() } });

    for (const title of PANELS)
      await expect.element(page.getByRole("slider", { name: title, exact: true })).toBeVisible();
    await expect
      .element(page.getByRole("slider", { name: "Glucose", exact: true }))
      .toHaveAttribute("aria-valuemax", "71");
    await expect.element(page.getByText("12 events").first()).toBeVisible();
    await expect.element(page.getByText(/1 left out for another event in its window/).first()).toBeVisible();
    await expect.element(page.getByText(/1 without a glucose reading at the time/).first()).toBeVisible();
  });

  it("is one tab stop, and reads each cohort's median and n at the slot", async () => {
    render(EventResponseTab, { props: { response: eventResponse() } });

    const sliders = page.getByRole("slider").elements();
    expect(sliders.filter((s) => s.getAttribute("tabindex") === "0")).toHaveLength(1);

    const slider = page.getByRole("slider", { name: "Glucose change from the event" });
    (slider.element() as HTMLElement).focus();
    await userEvent.keyboard("{Shift>}{ArrowRight}{/Shift}");
    await expect.element(slider).toHaveAttribute("aria-valuenow", "12");
    await expect
      .element(slider)
      .toHaveAttribute(
        "aria-valuetext",
        "Event to +5 min: Meals: median 0, 12 events; Unannounced rises: median +5, 3 events"
      );
    await userEvent.keyboard("{End}");
    await expect.element(slider).toHaveAttribute("aria-valuenow", "71");
    await userEvent.keyboard("{ArrowDown}");
    expect(document.activeElement?.getAttribute("aria-label")).toBe("Glucose");
  });

  it("compares each cohort's figures side by side, and lists every event with a Day in Review link", async () => {
    render(EventResponseTab, { props: { response: eventResponse({ comparison: true }) } });

    await expect.element(page.getByText("The comparison range is drawn as dashed outlines in each cohort's colour.")).toBeVisible();
    await expect.element(page.getByRole("rowheader", { name: "Observed grams per unit" })).toBeVisible();
    await expect.element(page.getByText("9.5 g/U").first()).toBeVisible();
    await expect.element(page.getByText(/Compared 9.5 g\/U/).first()).toBeVisible();

    const links = page.getByRole("link", { name: /in Day in Review/ }).elements();
    expect(links).toHaveLength(3);
    expect(links[0].getAttribute("href")).toBe("/reports/day-in-review?date=2026-10-10&time=07:30");
  });

  it("sorts the event list by a figure", async () => {
    render(EventResponseTab, { props: { response: eventResponse() } });

    await page.getByRole("button", { name: "Peak rise" }).click();
    const firstRow = () => page.getByRole("link", { name: /in Day in Review/ }).elements()[0].getAttribute("href");
    expect(firstRow()).toContain("2026-10-12");
    await expect.element(page.getByRole("columnheader", { name: "Peak rise" })).toHaveAttribute("aria-sort", "descending");
    await page.getByRole("button", { name: "Peak rise" }).click();
    expect(firstRow()).toContain("2026-10-11");
  });

  it("hides the event list and every treatment series the caller cannot read", async () => {
    render(EventResponseTab, { props: { response: eventResponse({ withheld: TREATMENT_SERIES }) } });

    await expect.element(page.getByRole("slider", { name: "Glucose", exact: true })).toBeVisible();
    expect(page.getByRole("slider", { name: "Insulin on board" }).elements()).toHaveLength(0);
    await expect.element(page.getByText("Hidden: your access does not include this data").first()).toBeVisible();
    expect(page.getByRole("link", { name: /in Day in Review/ }).elements()).toHaveLength(0);
    expect(page.getByRole("rowheader", { name: "Observed grams per unit" }).elements()).toHaveLength(0);
    await expect.element(page.getByRole("rowheader", { name: "Peak rise" })).toBeVisible();
  });

  it("hides the glucose series and figures without glucose", async () => {
    render(EventResponseTab, { props: { response: eventResponse({ withheld: [LoopAnalysisSeries.EventGlucose] }) } });

    expect(page.getByRole("slider", { name: "Glucose", exact: true }).elements()).toHaveLength(0);
    expect(page.getByRole("rowheader", { name: "Peak rise" }).elements()).toHaveLength(0);
    await expect.element(page.getByRole("slider", { name: "Insulin on board" })).toBeVisible();
  });

  it("explains a missing time zone in place of the curves", async () => {
    const response = eventResponse();
    response.timeZoneUnavailableReason = TimeZoneUnavailableReason.NotConfigured;
    render(EventResponseTab, { props: { response } });

    await expect.element(page.getByText("This report needs a time zone")).toBeVisible();
    expect(page.getByRole("slider").elements()).toHaveLength(0);
  });

  it("says when only the last 24 hours are included, and which choices were ignored", async () => {
    const response = eventResponse({ historyClamped: true });
    response.ignoredFilters = ["startDate", "anchor2", "isolatedOnly"];
    render(EventResponseTab, { props: { response } });

    await expect.element(page.getByText("Only the last 24 hours are included")).toBeVisible();
    await expect.element(page.getByText(/Cohort 2, Isolated events only\./)).toBeVisible();
  });

  it("says so when no cohort is left to align", async () => {
    const response = eventResponse();
    response.definition = { ...response.definition, cohorts: [] };
    render(EventResponseTab, { props: { response } });

    await expect.element(page.getByText("No events to align")).toBeVisible();
  });
});
