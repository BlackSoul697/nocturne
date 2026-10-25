import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-svelte";
import { page as browserPage } from "vitest/browser";
import { page } from "$app/state";
import { remoteQuery } from "$lib/test-stubs/remote-resource";

vi.mock("$lib/stores/public-clock-store.svelte", () => ({
  PublicClockStore: class {
    demoMode = false;
    watchAlerts() {}
    start() {}
    stop() {}
  },
}));
vi.mock("$lib/stores/realtime-store.svelte", () => ({ setClockGlucoseSource: () => {} }));
vi.mock("$lib/components/clock/ClockFaceRenderer.svelte", () => ({ default: () => {} }));
vi.mock("$lib/components/clock/ClockAlertBand.svelte", () => ({ default: () => {} }));
vi.mock("$api/generated/clockFaces.generated.remote", () => ({
  getById: () => remoteQuery(() => ({ id: "f", config: { rows: [], settings: {} } })),
}));

import ClockFaceView from "./+page@.svelte";

describe("clock face view", () => {
  beforeEach(() => {
    page.params.id = "11111111-1111-1111-1111-111111111111";
  });

  it("offers Edit to a caller holding sharing.manage", async () => {
    page.data.effectivePermissions = ["sharing.manage"];
    render(ClockFaceView);
    await expect.element(browserPage.getByRole("button", { name: "Edit" })).toBeInTheDocument();
  });

  it("withholds Edit from a caller without it", async () => {
    page.data.effectivePermissions = ["glucose.read"];
    render(ClockFaceView);
    await expect.element(browserPage.getByRole("button", { name: "Back" })).toBeInTheDocument();
    expect(browserPage.getByRole("button", { name: "Edit" }).query()).toBeNull();
  });

  it("shows the bar on a tap and hides it, untappable, a few seconds later", async () => {
    page.data.effectivePermissions = ["glucose.read"];
    render(ClockFaceView);
    const nav = browserPage.getByTestId("clock-nav");
    await expect.element(nav).toHaveAttribute("data-revealed", "false");

    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
    try {
      document.body.dispatchEvent(
        new PointerEvent("pointerdown", { pointerType: "touch", bubbles: true })
      );
      await expect.element(nav).toHaveAttribute("data-revealed", "true");
      expect(nav.element().hasAttribute("inert")).toBe(false);

      await vi.advanceTimersByTimeAsync(5000);
      await expect.element(nav).toHaveAttribute("data-revealed", "false");
      expect(nav.element().hasAttribute("inert")).toBe(true);
    } finally {
      vi.useRealTimers();
    }
  });
});
