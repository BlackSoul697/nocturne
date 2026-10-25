import { readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it, vi } from "vitest";
import { coachCopy } from "./copy.svelte";
import { ONBOARDING_CORE_GATE, sequences } from "./sequences";

vi.mock("$app/paths", () => ({ resolve: (path: string) => path }));

const SRC = fileURLToPath(new URL("../..", import.meta.url));
const TOUR = "tracker-intro";
const copy: Record<string, { title: string; description: string }> = coachCopy();
const steps = sequences[TOUR]?.steps ?? [];

/** Where each step's target is attached; the settings steps are on their own pages. */
const TARGETS: Record<string, string> = {
  "tracker-intro.pill": join("lib", "components", "status-pills", "TrackerPillBar.svelte"),
  "tracker-intro.complete": join("lib", "components", "status-pills", "TrackerPillBar.svelte"),
  "tracker-intro.settings": join("routes", "(authenticated)", "settings", "trackers", "+page.svelte"),
  "tracker-intro.rail": join("routes", "(authenticated)", "settings", "appearance", "+page.svelte"),
};

/** `sequences.test` proves every `coachFor` key sits in a `coachmark` call; this places each one. */
const attachedIn = (file: string, key: string) =>
  readFileSync(join(SRC, file), "utf8").includes(`coachFor("${key}")`);

describe("tracker introduction tour", () => {
  it("is registered under the name the legacy conversion dialog starts", () => {
    expect(sequences).toHaveProperty(TOUR);
  });

  it("tours the pill, its change control, the tracker settings and the rail settings in order", () => {
    expect(steps).toEqual(Object.keys(TARGETS));
  });

  it("waits for the onboarding core and gives way to the setup and feature tours", () => {
    expect(sequences[TOUR].prerequisite).toBe(ONBOARDING_CORE_GATE);
    expect(sequences[TOUR].priority).toBeLessThan(sequences["feature-intro"].priority);
  });

  it.each(Object.entries(TARGETS))("attaches %s in %s", (key, file) => {
    expect(attachedIn(file, key)).toBe(true);
  });

  it("has copy for every step", () => {
    for (const key of steps) {
      expect(copy[key]?.title, key).toBeTruthy();
      expect(copy[key]?.description, key).toBeTruthy();
    }
  });

  it("has no copy that no step shows", () => {
    const unused = Object.keys(copy).filter((key) => key.startsWith(`${TOUR}.`) && !steps.includes(key));

    expect(unused).toEqual([]);
  });

  it("writes every sentence in 20 words or fewer", () => {
    for (const key of steps)
      for (const sentence of copy[key].description.split(/(?<=\.)\s+/))
        expect(sentence.split(/\s+/).length, `${key}: ${sentence}`).toBeLessThanOrEqual(20);
  });

  it("tells the reader that Warning and Hazard alerts come from the thresholds", () => {
    expect(copy["tracker-intro.settings"].description).toContain(
      "Warning and Hazard alerts come from the notification thresholds."
    );
  });
});
