import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { describe, expect, it } from "vitest";
import { RailItemId } from "$lib/api/generated/nocturne-api-client";
import DashboardRailConfigurator from "./DashboardRailConfigurator.svelte";

const DEFAULT = [
  RailItemId.Cob,
  RailItemId.Basal,
  RailItemId.Iob,
  RailItemId.Loop,
  RailItemId.Reservoir,
  RailItemId.Trackers,
];

function setup(value: RailItemId[], defaultItems: RailItemId[] = DEFAULT) {
  const writes: RailItemId[][] = [];
  render(DashboardRailConfigurator, {
    props: { value, defaultItems, onchange: (items) => writes.push(items) },
  });
  return writes;
}

function shownOrder(): string[] {
  return [
    ...page.getByRole("list", { name: "Shown pills" }).element().querySelectorAll("[data-rail-item]"),
  ].map((row) => row.getAttribute("data-rail-item") ?? "");
}

describe("DashboardRailConfigurator", () => {
  it("lists the shown items in order and the rest as hidden", async () => {
    setup([RailItemId.Iob, RailItemId.Cob]);

    await expect.element(page.getByRole("list", { name: "Hidden pills" })).toBeVisible();
    expect(shownOrder()).toEqual([RailItemId.Iob, RailItemId.Cob]);
    const hidden = page.getByRole("list", { name: "Hidden pills" }).getByRole("listitem");
    expect(hidden.elements()).toHaveLength(4);
  });

  it("moves an item down", async () => {
    const writes = setup([RailItemId.Cob, RailItemId.Basal, RailItemId.Iob]);

    await page.getByRole("button", { name: "Move Carbs on board (COB) down" }).click();

    expect(writes.at(-1)).toEqual([RailItemId.Basal, RailItemId.Cob, RailItemId.Iob]);
  });

  it("reorders by drag and drop", async () => {
    const writes = setup([RailItemId.Cob, RailItemId.Basal, RailItemId.Iob]);
    const rows = page.getByRole("list", { name: "Shown pills" }).getByRole("listitem");

    await rows.nth(2).dropTo(rows.nth(0));

    expect(writes.at(-1)).toEqual([RailItemId.Iob, RailItemId.Cob, RailItemId.Basal]);
  });

  it("hides a shown item", async () => {
    const writes = setup([RailItemId.Cob, RailItemId.Trackers]);

    await page.getByRole("switch", { name: "Show Trackers" }).click();

    expect(writes.at(-1)).toEqual([RailItemId.Cob]);
  });

  it("shows a hidden item at the end of the rail", async () => {
    const writes = setup([RailItemId.Cob, RailItemId.Iob]);

    await page.getByRole("switch", { name: "Show Reservoir" }).click();

    expect(writes.at(-1)).toEqual([RailItemId.Cob, RailItemId.Iob, RailItemId.Reservoir]);
  });

  it("resets to the default it is given", async () => {
    const defaultItems = DEFAULT.filter((id) => id !== RailItemId.Trackers);
    const writes = setup([RailItemId.Loop], defaultItems);

    await page.getByRole("button", { name: "Reset to default" }).click();

    expect(writes.at(-1)).toEqual(defaultItems);
  });

  it("has nothing to reset while the rail is the default", async () => {
    setup(DEFAULT);

    await expect.element(page.getByRole("button", { name: "Reset to default" })).toBeDisabled();
  });
});
