import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { afterEach, describe, expect, it, vi } from "vitest";
import { WidgetId } from "$lib/api/generated/nocturne-api-client";
import { dashboardTopWidgets } from "$lib/stores/appearance-store.svelte";

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
      entries: [{ mills: now, sgv: 123 }],
      direction: "Flat",
      demoMode: false,
      pillsData: { cob: null, basal: null, iob: null, loop: null },
      trackerInstances: [] as unknown[],
      trackerDefinitions: [] as unknown[],
      timeSinceReading: Symbol("derived sentinel"),
      noteTreatmentWrite: () => {},
    },
    settings: { features: { trackerPills: { enabled: false } } },
  };
});

vi.mock("$lib/stores/realtime-store.svelte", () => ({
  getRealtimeStore: () => store,
  tryGetRealtimeStore: () => store,
}));

vi.mock("$lib/stores/settings-store.svelte", () => ({
  getSettingsStore: () => settings,
}));

// The value indicator uses Tooltip.Root, whose provider Sidebar.Provider supplies in the
// app layout; the wrapper stands in for it.
import CurrentBGDisplay from "./current-bg-display-test-wrapper.svelte";

describe("CurrentBGDisplay", () => {
  afterEach(() => {
    store.trackerInstances = [];
    store.trackerDefinitions = [];
    settings.features.trackerPills.enabled = false;
  });

  it("renders status text without reading a Symbol timeSinceReading value", () => {
    dashboardTopWidgets.hydrate([WidgetId.TirChart, WidgetId.Tdd]);
    expect(() => render(CurrentBGDisplay, { showPills: false })).not.toThrow();
  });

  it("keeps the reading in the header while the Current glucose widget is not showing", async () => {
    dashboardTopWidgets.hydrate([WidgetId.TirChart, WidgetId.Tdd]);
    render(CurrentBGDisplay, { showPills: false });

    await expect.element(page.getByText("123", { exact: true })).toBeInTheDocument();
  });

  it("hands the reading to the Current glucose widget when the grid shows it", async () => {
    dashboardTopWidgets.hydrate([WidgetId.TirChart, WidgetId.Tdd, WidgetId.BgDelta]);
    render(CurrentBGDisplay, { showPills: false });

    await expect.element(page.getByText("123", { exact: true })).not.toBeInTheDocument();
  });

  it("keeps the reading when the widget is stored past the grid's last slot", async () => {
    dashboardTopWidgets.hydrate([
      WidgetId.TirChart,
      WidgetId.Tdd,
      WidgetId.Clock,
      WidgetId.BgDelta,
    ]);
    render(CurrentBGDisplay, { showPills: false });

    await expect.element(page.getByText("123", { exact: true })).toBeInTheDocument();
  });

  it("loads the tracker completion dialog when a tracker pill is first completed", async () => {
    // The pills are hidden below the container's @md width.
    await page.viewport(1280, 900);
    settings.features.trackerPills.enabled = true;
    store.trackerDefinitions = [
      { id: "def-1", name: "Sensor", category: "Sensor", dashboardVisibility: "Always", lifespanHours: 240 },
    ];
    store.trackerInstances = [
      {
        id: "inst-1",
        definitionId: "def-1",
        definitionName: "Sensor",
        category: "Sensor",
        startedAt: new Date(now - 48 * 3600_000).toISOString(),
        ageHours: 48,
        isActive: true,
      },
    ];
    render(CurrentBGDisplay, { showPills: false });

    const dialog = page.getByTestId("tracker-completion-dialog");
    expect(dialog.elements()).toHaveLength(0);

    await page.getByRole("button", { name: /Sensor/ }).click();
    await page.getByRole("button", { name: "Complete Tracker" }).click();

    await expect.element(dialog).toBeVisible();
    await expect.element(dialog.getByText("Complete Sensor")).toBeVisible();
  });
});
