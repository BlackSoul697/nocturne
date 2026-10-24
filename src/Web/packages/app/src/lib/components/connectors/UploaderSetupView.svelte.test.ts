import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { UploaderPlatform, type UploaderApp } from "$api-clients";
import { remoteQuery } from "$lib/test-stubs/remote-resource";

const { create } = vi.hoisted(() => ({ create: vi.fn() }));

vi.mock("$lib/api/generated/directGrants.generated.remote", () => ({
  create: (...args: unknown[]) => create(...args),
}));
vi.mock("$api/generated/services.generated.remote", () => ({
  getActiveDataSources: () => remoteQuery(() => []),
}));
vi.mock("$routes/(authenticated)/oauth/oauth.remote", () => ({
  getDeviceInfo: vi.fn(),
}));
vi.mock("$api/generated/oAuths.generated.remote", () => ({
  deviceApprove: vi.fn(),
}));

import UploaderSetupView from "./UploaderSetupView.svelte";

const AAPS: UploaderApp = { id: "aaps", platform: UploaderPlatform.Android };
const MANUAL_SETUP = { app: AAPS, baseUrl: "https://example.test" };

const render_ = () =>
  render(UploaderSetupView, {
    app: AAPS,
    setupResponse: MANUAL_SETUP,
    onBack: () => {},
    onConnected: () => {},
  });

const pollTimers = (spy: { mock: { calls: unknown[][] } }) =>
  spy.mock.calls.filter((call) => call[1] === 15_000);

describe("UploaderSetupView manual setup", () => {
  let setIntervalSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    create.mockReset();
    setIntervalSpy = vi.spyOn(globalThis, "setInterval");
  });

  afterEach(() => {
    setIntervalSpy.mockRestore();
  });

  it("generates one key and starts polling once", async () => {
    create.mockResolvedValue({ token: "noc_created" });
    render_();

    await expect.element(page.getByText("noc_created")).toBeVisible();

    expect(create).toHaveBeenCalledTimes(1);
    expect(pollTimers(setIntervalSpy)).toHaveLength(1);
  });

  it("does not start polling when unmounted while the key is pending", async () => {
    let resolveKey: (value: { token: string }) => void = () => {};
    create.mockReturnValue(
      new Promise((resolve) => {
        resolveKey = resolve;
      })
    );
    const screen = render_();
    await expect.element(page.getByText("Generating API key...")).toBeVisible();

    await screen.unmount();
    resolveKey({ token: "noc_late" });
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(pollTimers(setIntervalSpy)).toHaveLength(0);
  });

  it("shows the error and does not retry when the key request fails", async () => {
    create.mockRejectedValue({
      status: 400,
      body: { message: "Label already used." },
    });
    render_();

    await expect.element(page.getByText("Label already used.")).toBeVisible();
    await new Promise((resolve) => setTimeout(resolve, 100));

    expect(create).toHaveBeenCalledTimes(1);
    expect(pollTimers(setIntervalSpy)).toHaveLength(0);
  });
});
