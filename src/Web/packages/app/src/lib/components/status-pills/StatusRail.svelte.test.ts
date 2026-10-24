import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { remoteQuery } from "$lib/test-stubs/remote-resource";
import { RailItemId } from "$lib/api/generated/nocturne-api-client";

const { now, store, settings } = vi.hoisted(() => {
  const now = Date.UTC(2026, 5, 14, 9, 30, 0);
  return {
    now,
    store: {
      currentBG: 123,
      currentEntry: { mills: now, sgv: 123 },
      bgDelta: 4,
      lastUpdated: now,
      now,
      isConnected: true,
      isReady: true,
      entries: [{ mills: now, sgv: 123 }],
      direction: "Flat",
      demoMode: false,
      pillsData: { cob: null, basal: null, iob: null, loop: null },
      currentReservoir: 120 as number | null,
      trackerInstances: [] as unknown[],
      trackerDefinitions: [] as unknown[],
    },
    settings: { features: { trackerPills: { enabled: true } } },
  };
});

vi.mock("$lib/stores/realtime-store.svelte", () => ({
  getRealtimeStore: () => store,
  tryGetRealtimeStore: () => store,
}));

vi.mock("$api/ui-settings.remote", () => ({
  getUiSettings: () => remoteQuery(() => settings),
}));

import { dashboardRail } from "$lib/stores/appearance-store.svelte";
import CurrentBGDisplay from "$lib/components/dashboard/current-bg-display-test-wrapper.svelte";

const SENSOR_DEFINITION = {
  id: "def-1",
  name: "Sensor",
  category: "Sensor",
  dashboardVisibility: "Always",
  lifespanHours: 240,
};
const SENSOR_INSTANCE = {
  id: "inst-1",
  definitionId: "def-1",
  definitionName: "Sensor",
  category: "Sensor",
  startedAt: new Date(now - 48 * 3600_000).toISOString(),
  ageHours: 48,
  isActive: true,
};

const DESKTOP = "status-pills";
// The pref writes through to localStorage, which every later test file's store reads at load.
const RAIL_STORAGE_KEY = "nocturne-dashboard-rail";
const MOBILE = "status-pills-mobile";

/** The pill labels a rail shows, in the order it shows them. */
function railLabels(testId: string): string[] {
  const rail = page.getByTestId(testId).element();
  return [...rail.querySelectorAll("span.text-xs")].map(
    (label) => label.textContent?.trim() ?? ""
  );
}

function hasLabel(labels: string[], name: string): boolean {
  return labels.some((label) => label.startsWith(name));
}

function indexOfLabel(labels: string[], name: string): number {
  return labels.findIndex((label) => label.startsWith(name));
}

describe("StatusRail", () => {
  beforeEach(async () => {
    await page.viewport(1280, 900);
    dashboardRail.presentFrom({});
    settings.features.trackerPills.enabled = true;
    store.currentReservoir = 120;
    store.trackerDefinitions = [SENSOR_DEFINITION];
    store.trackerInstances = [SENSOR_INSTANCE];
  });

  afterEach(() => {
    dashboardRail.presentFrom({});
    localStorage.removeItem(RAIL_STORAGE_KEY);
    store.trackerInstances = [];
    store.trackerDefinitions = [];
  });

  it("shows the stored items in the stored order", async () => {
    dashboardRail.hydrate([RailItemId.Reservoir, RailItemId.Iob, RailItemId.Cob]);
    render(CurrentBGDisplay);

    await expect.element(page.getByTestId(DESKTOP).getByText("Reservoir")).toBeVisible();
    const labels = railLabels(DESKTOP);
    expect(indexOfLabel(labels, "Reservoir")).toBeLessThan(indexOfLabel(labels, "IOB"));
    expect(indexOfLabel(labels, "IOB")).toBeLessThan(indexOfLabel(labels, "COB"));
  });

  it("does not show an item the user hid", async () => {
    dashboardRail.hydrate([RailItemId.Cob, RailItemId.Iob]);
    render(CurrentBGDisplay);

    await expect.element(page.getByTestId(DESKTOP).getByText("COB")).toBeVisible();
    const labels = railLabels(DESKTOP);
    expect(hasLabel(labels, "Basal")).toBe(false);
    expect(hasLabel(labels, "Loop")).toBe(false);
    expect(hasLabel(labels, "Reservoir")).toBe(false);
    expect(page.getByRole("button", { name: /Sensor/ }).elements()).toHaveLength(0);
  });

  it("shows the default with trackers when nothing is stored and the tenant shows tracker pills", async () => {
    render(CurrentBGDisplay);

    await expect.element(page.getByTestId(DESKTOP).getByText("Reservoir")).toBeVisible();
    const labels = railLabels(DESKTOP);
    for (const name of ["COB", "Basal", "IOB", "Loop", "Reservoir"]) {
      expect(hasLabel(labels, name), name).toBe(true);
    }
    await expect
      .element(page.getByTestId(DESKTOP).getByRole("button", { name: /Sensor/ }))
      .toBeVisible();
  });

  it("leaves trackers out of the default when the tenant hides tracker pills", async () => {
    settings.features.trackerPills.enabled = false;
    render(CurrentBGDisplay);

    await expect.element(page.getByTestId(DESKTOP).getByText("Reservoir")).toBeVisible();
    expect(page.getByRole("button", { name: /Sensor/ }).elements()).toHaveLength(0);
  });

  it("shows trackers a user stored even when the tenant hides tracker pills", async () => {
    settings.features.trackerPills.enabled = false;
    dashboardRail.hydrate([RailItemId.Trackers]);
    render(CurrentBGDisplay);

    await expect
      .element(page.getByTestId(DESKTOP).getByRole("button", { name: /Sensor/ }))
      .toBeVisible();
  });

  it("keeps the reservoir out while the pump reports none", async () => {
    store.currentReservoir = null;
    dashboardRail.hydrate([RailItemId.Reservoir, RailItemId.Cob]);
    render(CurrentBGDisplay);

    await expect.element(page.getByTestId(DESKTOP).getByText("COB")).toBeVisible();
    expect(hasLabel(railLabels(DESKTOP), "Reservoir")).toBe(false);
  });

  it("shows the same rail, trackers included, in the mobile layout", async () => {
    await page.viewport(400, 900);
    dashboardRail.hydrate([RailItemId.Trackers, RailItemId.Loop, RailItemId.Cob]);
    render(CurrentBGDisplay);

    const mobile = page.getByTestId(MOBILE);
    await expect.element(mobile.getByRole("button", { name: /Sensor/ })).toBeVisible();
    await expect.element(page.getByTestId(DESKTOP)).not.toBeVisible();
    const labels = railLabels(MOBILE);
    expect(indexOfLabel(labels, "Loop")).toBeLessThan(indexOfLabel(labels, "COB"));
    expect(hasLabel(labels, "IOB")).toBe(false);
  });

  it("hides the mobile layout at desktop width", async () => {
    render(CurrentBGDisplay);

    await expect.element(page.getByTestId(DESKTOP)).toBeVisible();
    await expect.element(page.getByTestId(MOBILE)).not.toBeVisible();
  });
});
