import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { page as pageState } from "$app/state";
import {
  LegacyConsumableKind,
  NotificationUrgency,
  TrackerCategory,
  type LegacyFallbackDto,
} from "$lib/api/generated/nocturne-api-client";
import { hiddenLegacyFallbacks } from "$lib/stores/appearance-store.svelte";
import LegacyAgePill from "./LegacyAgePill.svelte";

const HOUR = 3600_000;
const now = Date.UTC(2026, 9, 25, 12, 0, 0);
const at = (hoursAgo: number) => new Date(now - hoursAgo * HOUR).toISOString();

// The pref writes through to localStorage, which every later test file's store reads at load.
const HIDDEN_STORAGE_KEY = "nocturne-hidden-legacy-fallbacks";

const SITE: LegacyFallbackDto = {
  kind: LegacyConsumableKind.Site,
  latestEventAt: at(50),
  ageHours: 50,
  schedule: [
    { urgency: NotificationUrgency.Info, firesAt: at(6) },
    { urgency: NotificationUrgency.Warn, firesAt: at(2) },
    { urgency: NotificationUrgency.Urgent, firesAt: new Date(now + 22 * HOUR).toISOString() },
  ],
  spec: { name: "Infusion Site", category: TrackerCategory.Cannula, lifespanHours: 72 },
};

const pill = () => page.getByRole("button", { name: /Infusion Site/ });
const createButton = () => page.getByRole("button", { name: "Create a tracker" });
const hideButton = () => page.getByRole("button", { name: "Hide" });

describe("LegacyAgePill", () => {
  beforeEach(() => {
    hiddenLegacyFallbacks.presentFrom({});
    pageState.data = { effectivePermissions: ["alerts.readwrite"] };
  });

  afterEach(() => {
    hiddenLegacyFallbacks.presentFrom({});
    localStorage.removeItem(HIDDEN_STORAGE_KEY);
  });

  it("shows the age in the tone of the step the schedule reached", async () => {
    render(LegacyAgePill, { fallback: SITE, now });

    await expect.element(pill()).toHaveTextContent("2d 2h");
    await expect.element(pill().getByText("Warning:")).toBeInTheDocument();
  });

  it("shows the age and the last change in its popover", async () => {
    render(LegacyAgePill, { fallback: SITE, now });

    await pill().click();

    const popover = page.getByTestId("legacy-age-pill-popover");
    await expect.element(popover.getByText("Last change", { exact: true })).toBeVisible();
    await expect.element(popover.getByText("3 days")).toBeVisible();
  });

  it("offers a member with alerts.readwrite to create a tracker for this kind", async () => {
    const onCreateTracker = vi.fn();
    render(LegacyAgePill, { fallback: SITE, now, onCreateTracker });

    await pill().click();
    await createButton().click();

    expect(onCreateTracker).toHaveBeenCalledWith(LegacyConsumableKind.Site);
  });

  it("hides the kind for this user", async () => {
    hiddenLegacyFallbacks.hydrate([LegacyConsumableKind.Sensor]);
    render(LegacyAgePill, { fallback: SITE, now });

    await pill().click();
    await hideButton().click();

    expect(hiddenLegacyFallbacks.current).toEqual([
      LegacyConsumableKind.Sensor,
      LegacyConsumableKind.Site,
    ]);
  });

  it("shows a member without alerts.readwrite the age and no actions", async () => {
    pageState.data = { effectivePermissions: ["alerts.read", "devices.read"] };
    render(LegacyAgePill, { fallback: SITE, now });

    await pill().click();

    await expect.element(page.getByTestId("legacy-age-pill-popover")).toBeVisible();
    await expect.element(createButton()).not.toBeInTheDocument();
    await expect.element(hideButton()).not.toBeInTheDocument();
  });
});
