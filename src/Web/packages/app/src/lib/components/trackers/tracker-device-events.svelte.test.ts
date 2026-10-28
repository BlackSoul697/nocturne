import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  DeviceEventType,
  TrackerCategory,
  TrackerMode,
} from "#lib/api/generated/nocturne-api-client.js";

const { createDeviceEvent } = vi.hoisted(() => ({
  createDeviceEvent: vi.fn(async (request: unknown) => request),
}));

vi.mock("#lib/api/generated/trackers.generated.remote.js", async () => {
  const { remoteCommand } = await import("#lib/test-stubs/remote-resource.js");
  return {
    startInstance: remoteCommand(async () => ({})),
    completeInstance: remoteCommand(async () => ({})),
  };
});

// Built with the `form` the generated module imports, so a submission meets the schema as the
// generated `create` does.
vi.mock("#lib/api/generated/deviceEvents.generated.remote.js", async () => {
  const { form } = await import("#lib/api/remote-server.js");
  const { formCoerce } = await import("#lib/api/generated/form-utils.generated.js");
  const { UpsertDeviceEventRequestSchema } = await import("#lib/api/generated/schemas.js");
  return { create: form(formCoerce(UpsertDeviceEventRequestSchema), createDeviceEvent) };
});

import TrackerStartDialog from "./TrackerStartDialog.svelte";
import TrackerCompletionDialog from "./TrackerCompletionDialog.svelte";

describe("tracker dialogs post their device event", () => {
  beforeEach(() => createDeviceEvent.mockClear());

  it("posts the start event when a tracker starts", async () => {
    render(TrackerStartDialog, {
      open: true,
      definition: {
        id: "0192f0e4-0000-7000-8000-000000000001",
        name: "G7",
        category: TrackerCategory.Sensor,
        mode: TrackerMode.Duration,
        startEventType: DeviceEventType.SensorStart,
        notificationThresholds: [],
      },
      onClose: () => {},
    });

    await page.getByRole("button", { name: "Start" }).click();

    await vi.waitFor(() => expect(createDeviceEvent).toHaveBeenCalledOnce());
    expect(createDeviceEvent.mock.calls[0][0]).toMatchObject({
      eventType: DeviceEventType.SensorStart,
      app: "Nocturne Tracker",
    });
  });

  it("posts the completion event when a tracker completes", async () => {
    render(TrackerCompletionDialog, {
      open: true,
      instanceId: "0192f0e4-0000-7000-8000-000000000002",
      category: TrackerCategory.Sensor,
      completionEventType: DeviceEventType.SensorStop,
      onClose: () => {},
    });

    await page.getByRole("button", { name: "Complete", exact: true }).click();

    await vi.waitFor(() => expect(createDeviceEvent).toHaveBeenCalledOnce());
    expect(createDeviceEvent.mock.calls[0][0]).toMatchObject({
      eventType: DeviceEventType.SensorStop,
      app: "Nocturne Tracker",
    });
  });
});
