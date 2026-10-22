import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { EventAnchorKind, type EventResponseDefinition } from "$lib/api";
import EventCohortBuilder from "./EventCohortBuilder.svelte";
import type { EventResponseState } from "./event-response-filter";

const applied: EventResponseDefinition = {
  cohorts: [{ cohort: 1, anchor: EventAnchorKind.Meal }],
  minutesBefore: 60,
  minutesAfter: 300,
};

describe("EventCohortBuilder", () => {
  it("shows the cohorts the API applied while the URL names none, and writes them out on a change", async () => {
    const onchange = vi.fn<(next: EventResponseState) => void>();
    render(EventCohortBuilder, { props: { value: { cohorts: [] }, onchange, applied } });

    await expect.element(page.getByLabelText("Cohort 1", { exact: true })).toHaveTextContent("Meals");
    await expect.element(page.getByLabelText("At least (g)")).toBeVisible();

    await page.getByRole("button", { name: "Add cohort" }).click();
    expect(onchange).toHaveBeenLastCalledWith(
      expect.objectContaining({
        cohorts: [
          { cohort: 1, anchor: EventAnchorKind.Meal },
          { cohort: 2, anchor: EventAnchorKind.CorrectionBolus },
        ],
      })
    );
  });

  it("offers a cohort's grams only for meals", async () => {
    render(EventCohortBuilder, {
      props: { value: { cohorts: [{ cohort: 1, anchor: EventAnchorKind.SensorStart }] }, onchange: () => {}, applied },
    });

    expect(page.getByLabelText("At least (g)").elements()).toHaveLength(0);
    await expect.element(page.getByLabelText("Cohort 1 from")).toBeVisible();
  });

  it("offers corroboration only for unannounced rises, and disables what the viewer cannot use", async () => {
    render(EventCohortBuilder, {
      props: {
        value: { cohorts: [{ cohort: 1, anchor: EventAnchorKind.UnannouncedRise }] },
        onchange: () => {},
        unavailableFilters: [{ filter: "isolatedOnly", requiredScope: "treatments.read" }],
      },
    });

    await expect.element(page.getByLabelText("Only rises the loop answered with its own correction")).toBeVisible();
    await expect.element(page.getByLabelText("Isolated events only")).toBeDisabled();
    await expect.element(page.getByText("(Needs access to treatments)")).toBeVisible();
  });

  it("never writes a definition the API would refuse", async () => {
    const onchange = vi.fn<(next: EventResponseState) => void>();
    render(EventCohortBuilder, {
      props: { value: { cohorts: [{ cohort: 1, anchor: EventAnchorKind.Meal, fromMinute: 300, minGrams: 80 }] }, onchange },
    });

    const change = (label: string, value: string) => {
      const input = page.getByLabelText(label, { exact: true }).element() as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event("change", { bubbles: true }));
    };

    change("Cohort 1 to", "05:00");
    expect(onchange).not.toHaveBeenCalled();
    await expect.element(page.getByText("Pick a start and an end that differ, or clear both for all day.")).toBeVisible();

    change("At most (g)", "20");
    const written = onchange.mock.lastCall![0].cohorts![0];
    expect([written.minGrams, written.maxGrams]).toEqual([20, 80]);
    expect(written.fromMinute).toBe(300);
  });

  it("says so when the viewer can align on nothing", async () => {
    render(EventCohortBuilder, {
      props: {
        value: { cohorts: [] },
        onchange: () => {},
        applied: { cohorts: [] },
        unavailableAnchors: Object.values(EventAnchorKind).map((anchor) => ({ anchor, requiredScope: "treatments.read" })),
      },
    });

    await expect.element(page.getByText("Your access includes none of the events this tab aligns on.")).toBeVisible();
    expect(page.getByRole("button", { name: "Add cohort" }).elements()).toHaveLength(0);
  });
});
