import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "svelte/server";
import { page as pageState } from "$app/state";

const calls = vi.hoisted(() => ({ fetched: [] as string[] }));

vi.mock("$api/generated/loopAnalysis.generated.remote", () => {
  const remote = (name: string) => () => {
    calls.fetched.push(name);
    return { loading: true, error: null, current: undefined, refresh: () => {} };
  };
  return {
    getDailyProfile: remote("daily-profile"),
    getEventResponse: remote("meals-events"),
    getPredictionAccuracy: remote("predictions"),
    getLoopHealth: remote("loop-health"),
  };
});

// The tabs' charts are not rendered without data, and their chart library does not load in node.
vi.mock("$lib/components/reports/loop-analysis/DailyProfileTab.svelte", async () => ({
  default: (await import("$lib/test-stubs/Empty.test-stub.svelte")).default,
}));
vi.mock("$lib/components/reports/loop-analysis/EventResponseTab.svelte", async () => ({
  default: (await import("$lib/test-stubs/Empty.test-stub.svelte")).default,
}));
vi.mock("$lib/components/reports/loop-analysis/EventCohortBuilder.svelte", async () => ({
  default: (await import("$lib/test-stubs/Empty.test-stub.svelte")).default,
}));
vi.mock("$lib/components/reports/loop-analysis/PredictionsTab.svelte", async () => ({
  default: (await import("$lib/test-stubs/Empty.test-stub.svelte")).default,
}));
vi.mock("$lib/components/reports/loop-analysis/LoopHealthTab.svelte", async () => ({
  default: (await import("$lib/test-stubs/Empty.test-stub.svelte")).default,
}));
vi.mock("$lib/components/reports/loop-analysis/LoopAnalysisFilterBar.svelte", async () => ({
  default: (await import("$lib/test-stubs/Empty.test-stub.svelte")).default,
}));

vi.mock("$app/paths", () => ({ resolve: (path: string) => path }));

vi.mock("$lib/hooks/date-params.svelte", () => ({
  requireDateParamsContext: () => ({ fromDay: "2026-10-09", toDay: "2026-10-22" }),
}));

vi.mock("$lib/hooks/resource-context.svelte", () => ({
  contextResource: <T>(create: () => { current: T | undefined }) => {
    const query = create();
    return { current: query.current, stale: false, error: null };
  },
}));

import LoopAnalysisPage from "./+page.svelte";

describe("Loop Analysis page on the server", () => {
  beforeEach(() => {
    calls.fetched.length = 0;
  });

  it.each(["daily-profile", "meals-events", "predictions", "loop-health"])(
    "fetches the %s tab it opens on while rendering, and no other",
    (tab) => {
      pageState.url = new URL(`http://localhost/reports/loop-analysis${tab === "daily-profile" ? "" : `?tab=${tab}`}`) as typeof pageState.url;

      // Reading the body runs the render.
      const { body } = render(LoopAnalysisPage);

      expect(body).toContain('role="tablist"');
      expect(calls.fetched).toEqual([tab]);
    }
  );
});
