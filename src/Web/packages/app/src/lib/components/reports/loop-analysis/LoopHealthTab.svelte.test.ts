import { render } from "vitest-browser-svelte";
import { page, userEvent } from "vitest/browser";
import { describe, expect, it } from "vitest";
import { TimeZoneUnavailableReason } from "$lib/api";
import LoopHealthTab from "./LoopHealthTab.svelte";
import { loopHealthResponse } from "./loop-health.test-fixtures";

/** The tile with this label, as the figure strip lays it out. */
const tile = (label: string) =>
  page.elementLocator(page.getByRole("term").getByText(label, { exact: true }).element().parentElement!);

const STRIPS = ["Closed loop by hour", "Algorithm zero temps by hour", "At max basal by hour", "Sensitivity ratio by hour"];

describe("LoopHealthTab", () => {
  it("shows every figure as the API measured it, and the hour strips", async () => {
    render(LoopHealthTab, { props: { response: loopHealthResponse() } });

    await expect.element(tile("Closed loop")).toHaveTextContent("91.1%");
    await expect.element(tile("Closed loop")).toHaveTextContent("AID active 97.5% over the whole range, unfiltered");
    await expect.element(tile("CGM active")).toHaveTextContent("98.7%");
    await expect.element(tile("Pump active")).toHaveTextContent("92.4%");
    await expect.element(tile("Loop gaps")).toHaveTextContent(/2\s*stretches/);
    await expect.element(tile("Loop gaps")).toHaveTextContent("1 h 55 min in all, longest 1 h 30 min");
    await expect.element(tile("Unrecorded delivery")).toHaveTextContent("5 h in all");
    await expect.element(tile("Algorithm zero temps")).toHaveTextContent("41.0%");
    await expect.element(tile("Algorithm zero temps")).toHaveTextContent("Suspended 1.2%, manual zero temps 0.0%");
    await expect.element(tile("At max basal")).toHaveTextContent("2.5%");
    await expect.element(tile("At max basal")).toHaveTextContent("Cap known for 76.7% of algorithm temp time");
    await expect.element(tile("At max IOB")).toHaveTextContent("0.1%");
    await expect.element(tile("Automatic boluses")).toHaveTextContent(/54\.0\s*per day/);
    await expect.element(tile("Automatic boluses")).toHaveTextContent("Median 0.20 U");
    await expect.element(tile("Suggested, not enacted")).toHaveTextContent("0.2%");
    await expect.element(tile("Max IOB")).toHaveTextContent(/15\.0\s*U/);

    for (const title of STRIPS) await expect.element(page.getByRole("slider", { name: title })).toBeVisible();
  });

  it("lists the gaps longest first, each linked to Day in Review where it began", async () => {
    render(LoopHealthTab, { props: { response: loopHealthResponse() } });

    const links = page.getByRole("link");
    await expect.element(links.first()).toHaveAttribute("href", "/reports/day-in-review?date=2026-10-08&time=06:00");
    await expect.element(links.first()).toHaveTextContent("Unrecorded delivery");
    await expect.element(links.nth(1)).toHaveAttribute("href", "/reports/day-in-review?date=2026-10-03&time=23:30");
    await expect.element(links.nth(2)).toHaveAttribute("href", "/reports/day-in-review?date=2026-10-05&time=03:10");
  });

  it("narrows the gap list to an hour picked on a strip", async () => {
    render(LoopHealthTab, { props: { response: loopHealthResponse() } });
    const strip = page.getByRole("slider", { name: "Closed loop by hour" });
    (strip.element() as HTMLElement).focus();
    for (let i = 0; i < 3; i++) await userEvent.keyboard("{Shift>}{ArrowRight}{/Shift}");
    await userEvent.keyboard("{Enter}");

    await expect.element(page.getByRole("heading", { name: /Gaps that began/ })).toBeVisible();
    expect(page.getByRole("link").elements()).toHaveLength(1);
    await expect.element(page.getByRole("link")).toHaveAttribute("href", expect.stringContaining("time=03:10"));

    await page.getByRole("button", { name: "Show every hour" }).click();
    expect(page.getByRole("link").elements()).toHaveLength(3);
  });

  it("reads a cap no cycle vouched for as unknown, never as zero", async () => {
    render(LoopHealthTab, { props: { response: loopHealthResponse({ capUnknown: true }) } });

    await expect.element(tile("At max basal")).toHaveTextContent("Unknown");
    await expect
      .element(tile("At max basal"))
      .toHaveTextContent("The loop did not record which limit was in force for any temp");
    await expect.element(page.getByText("Unknown: no algorithm temp's cap is known in this range")).toBeVisible();
  });

  it("reads Loop's missing oref figures and oref's missing Loop setting as unknown, with why", async () => {
    render(LoopHealthTab, { props: { response: loopHealthResponse({ system: "loop" }) } });

    await expect.element(tile("At max IOB")).toHaveTextContent("Unknown");
    await expect.element(tile("At max IOB")).toHaveTextContent("Only oref systems (AAPS, Trio, OpenAPS)");
    await expect.element(tile("Suggested, not enacted")).toHaveTextContent("Unknown");
    await expect.element(tile("Max IOB")).toHaveTextContent("Unknown");
    await expect.element(tile("Loop max basal rate")).toHaveTextContent(/3\.00\s*U\/h/);
    await expect.element(page.getByText("No sensitivity ratio reported in this range")).toBeVisible();
  });

  it("reads an oref range's unrecorded Loop setting as unknown", async () => {
    render(LoopHealthTab, { props: { response: loopHealthResponse({ system: "oref" }) } });

    await expect.element(tile("Loop max basal rate")).toHaveTextContent("Unknown");
    await expect.element(tile("Loop max basal rate")).toHaveTextContent("Only Loop records its maximum basal rate setting");
  });

  it("hides what the caller's access withholds, and says so", async () => {
    render(LoopHealthTab, {
      props: { response: loopHealthResponse({ scopes: ["glucose.read", "treatments.read", "reports.read"] }) },
    });

    for (const label of ["Closed loop", "Loop gaps", "At max basal", "At max IOB", "Max IOB", "Loop max basal rate"])
      await expect.element(tile(label)).toHaveTextContent("Hidden: your access does not include this data");
    await expect.element(tile("CGM active")).toHaveTextContent("98.7%");
    await expect.element(tile("Algorithm zero temps")).toHaveTextContent("41.0%");
    expect(page.getByRole("slider", { name: "Closed loop by hour" }).elements()).toHaveLength(0);
    await expect.element(page.getByRole("slider", { name: "Algorithm zero temps by hour" })).toBeVisible();
    expect(page.getByRole("link").elements()).toHaveLength(0);
  });

  it("withholds max basal without therapy settings, since Loop's hours at its cap show the setting", async () => {
    render(LoopHealthTab, {
      props: { response: loopHealthResponse({ scopes: ["glucose.read", "treatments.read", "devices.read", "reports.read"] }) },
    });

    await expect.element(tile("At max basal")).toHaveTextContent("Hidden: your access does not include this data");
    await expect.element(tile("Loop gaps")).toHaveTextContent(/2\s*stretches/);
  });

  it("puts the comparison beside each figure and over each hour", async () => {
    render(LoopHealthTab, { props: { response: loopHealthResponse({ comparison: true }) } });

    await expect.element(tile("Closed loop")).toHaveTextContent("Compared: 45.5%");
    await expect.element(tile("Automatic boluses")).toHaveTextContent("Compared: 27.0 per day");
    await expect.element(page.getByText("The comparison range is drawn as dashed outlines over each hour.")).toBeVisible();

    const strip = page.getByRole("slider", { name: "Closed loop by hour" });
    (strip.element() as HTMLElement).focus();
    await expect.element(page.getByRole("tooltip")).toHaveTextContent("Compared");
  });

  it("explains a missing time zone in place of every figure", async () => {
    render(LoopHealthTab, {
      props: {
        response: {
          ...loopHealthResponse(),
          primary: { days: [] },
          timeZoneUnavailableReason: TimeZoneUnavailableReason.NotConfigured,
        },
      },
    });

    await expect.element(page.getByText("This report needs a time zone")).toBeVisible();
    expect(page.getByText("Closed loop", { exact: true }).elements()).toHaveLength(0);
  });

  it("says a clamped viewer sees only the last 24 hours", async () => {
    render(LoopHealthTab, { props: { response: loopHealthResponse({ historyClamped: true, days: 1 }) } });

    await expect.element(page.getByText("Only the last 24 hours are included")).toBeVisible();
    await expect.element(tile("Closed loop")).toHaveTextContent("91.1%");
  });

  it("reads each hour's share and days as the strip's value", async () => {
    render(LoopHealthTab, { props: { response: loopHealthResponse() } });

    await expect
      .element(page.getByRole("slider", { name: "Closed loop by hour" }))
      .toHaveAttribute("aria-valuetext", expect.stringContaining("91.1%, 14 days"));
  });
});
