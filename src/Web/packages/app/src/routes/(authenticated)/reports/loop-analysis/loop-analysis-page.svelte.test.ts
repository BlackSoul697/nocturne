import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { page as pageState } from "$app/state";
import type { DailyProfileResponse, LoopHealthResponse } from "$lib/api";
import { remoteQuery } from "$lib/test-stubs/remote-resource";
import {
  ALL_SCOPES,
  profileResponse,
} from "$lib/components/reports/loop-analysis/daily-profile.test-fixtures";
import { loopHealthResponse } from "$lib/components/reports/loop-analysis/loop-health.test-fixtures";

const calls = vi.hoisted(() => ({
  query: [] as unknown[],
  loopHealth: [] as unknown[],
  goto: [] as [string, unknown][],
}));
let response: DailyProfileResponse;
let healthResponse: LoopHealthResponse;
let resourceState: { stale?: boolean; error?: unknown } = {};

vi.mock("$api/generated/loopAnalysis.generated.remote", () => ({
  getDailyProfile: (input: unknown) => {
    calls.query.push(input);
    return remoteQuery(() => response);
  },
  getLoopHealth: (input: unknown) => {
    calls.loopHealth.push(input);
    return remoteQuery(() => healthResponse);
  },
}));

vi.mock("$app/navigation", () => ({
  goto: (url: string, options?: unknown) => {
    calls.goto.push([url, options]);
    return Promise.resolve();
  },
}));

vi.mock("$lib/hooks/date-params.svelte", () => ({
  requireDateParamsContext: () => ({ fromDay: "2026-10-09", toDay: "2026-10-22" }),
}));

vi.mock("$lib/hooks/resource-context.svelte", () => ({
  contextResource: <T>(create: () => { current: T | undefined }) => ({
    get current() {
      return create().current;
    },
    get stale() {
      return resourceState.stale ?? false;
    },
    get error() {
      return resourceState.error;
    },
  }),
}));

import LoopAnalysisPage from "./+page.svelte";

function open(search: string, data: Record<string, unknown> = {}) {
  pageState.url = new URL(`http://localhost/reports/loop-analysis${search}`) as typeof pageState.url;
  for (const key of Object.keys(pageState.data)) delete pageState.data[key];
  Object.assign(pageState.data, { effectivePermissions: ALL_SCOPES, ...data });
  return render(LoopAnalysisPage);
}

const lastQuery = () => calls.query.at(-1);

describe("Loop Analysis page", () => {
  beforeEach(() => {
    calls.query.length = 0;
    calls.loopHealth.length = 0;
    calls.goto.length = 0;
    response = profileResponse();
    healthResponse = loopHealthResponse();
    resourceState = {};
  });

  it("shows what was picked, not loading, when the newer request failed", async () => {
    response = {
      ...profileResponse(),
      primaryRange: { startDate: "2026-07-25", endDate: "2026-10-22", shortened: true },
    };
    resourceState = { stale: true, error: new Error("Request rejected") };
    open("?excludeIllness=false");

    await expect.element(page.getByText("Oct 9 – 22, 2026")).toBeVisible();
    await expect.element(page.getByRole("group", { name: "Exclude" })).toHaveAttribute("aria-busy", "false");
    await expect.element(page.getByLabelText("Illness")).toBeEnabled();
    await expect.element(page.getByLabelText("Illness")).not.toBeChecked();
    await expect.element(page.getByLabelText("Exercise")).toBeEnabled();
    expect(page.getByText(/covers at most 90 days/).elements()).toHaveLength(0);
  });

  it("round-trips the filters through the URL it navigates to", async () => {
    const first = open("?days=14&isDefault=false&weekdays=6");
    expect(lastQuery()).toEqual({ startDate: "2026-10-09", endDate: "2026-10-22", days: [6] });

    await page.getByLabelText("Exercise").click();
    const target = calls.goto.at(-1);
    expect(target).toBeDefined();
    const search = new URL(target![0], "http://localhost").searchParams;
    expect(search.get("excludeExercise")).toBe("true");
    expect(search.get("days")).toBe("14");
    expect(search.get("weekdays")).toBe("6");
    first.unmount();

    open(`?${search}`);
    expect(lastQuery()).toEqual({
      startDate: "2026-10-09",
      endDate: "2026-10-22",
      days: [6],
      excludeExercise: true,
    });
    await expect.element(page.getByLabelText("Exercise")).toBeChecked();
  });

  it("shows the range as the API resolved it, not as it was picked", async () => {
    response = {
      ...profileResponse(),
      primaryRange: {
        startDate: "2026-08-04",
        endDate: "2026-11-01",
        from: "2026-08-04T04:00:00Z",
        to: "2026-11-02T05:00:00Z",
        shortened: true,
      },
    };
    open("");

    expect(lastQuery()).toEqual({ startDate: "2026-10-09", endDate: "2026-10-22" });
    await expect.element(page.getByText("Aug 4 – Nov 1, 2026")).toBeVisible();
    await expect.element(page.getByText(/covers at most 90 days/)).toBeVisible();
  });

  it("asks for a previous period and shows the comparison the API trimmed", async () => {
    response = {
      ...profileResponse({ comparison: true }),
      comparisonRange: { startDate: "2026-05-06", endDate: "2026-08-03", shortened: true },
    };
    open("?compare=previous");

    expect(lastQuery()).toMatchObject({ compare: "Previous" });
    await expect.element(page.getByText("May 6 – Aug 3, 2026")).toBeVisible();
    await expect.element(page.getByText(/covers at most 90 days/)).toBeVisible();
  });

  it("drops from the URL, replacing it, a filter the viewer cannot use", async () => {
    response = profileResponse({ scopes: ["glucose.read", "treatments.read", "therapy.read", "reports.read"] });
    open("?days=14&excludeOpenLoop=true&excludeIllness=false");

    await expect.poll(() => calls.goto.length).toBeGreaterThan(0);
    const [url, options] = calls.goto.at(-1)!;
    const search = new URL(url, "http://localhost").searchParams;
    expect(search.has("excludeOpenLoop")).toBe(false);
    expect(search.get("excludeIllness")).toBe("false");
    expect(search.get("days")).toBe("14");
    expect(options).toMatchObject({ replaceState: true });
  });

  it("leaves a URL alone when every filter it sets is usable", async () => {
    open("?days=14&excludeIllness=false");
    await expect.element(page.getByLabelText("Illness")).toBeVisible();
    expect(calls.goto).toHaveLength(0);
  });

  it("shows a clamped viewer the last 24 hours whatever range is picked", async () => {
    response = {
      ...profileResponse({ historyClamped: true }),
      primaryRange: {
        startDate: "2026-10-21",
        endDate: "2026-10-22",
        from: "2026-10-21T09:00:00Z",
        to: "2026-10-22T09:00:00Z",
        shortened: false,
      },
      comparisonRange: undefined,
      ignoredFilters: ["startDate", "endDate", "compare"],
    };
    open("?from=2026-03-01&to=2026-03-14&isDefault=false&compare=previous", { limitTo24Hours: true });

    await expect.element(page.getByText(/You can see the last 24 hours only/)).toBeVisible();
    await expect.element(page.getByText("Only the last 24 hours are included")).toBeVisible();
    expect(page.getByText(/Mar 1/).elements()).toHaveLength(0);
    expect(page.getByText("Some filters were not applied").elements()).toHaveLength(0);
  });

  it("fetches only Loop health on its tab, with the same filter", async () => {
    open("?tab=loop-health&weekdays=6&excludeIllness=false");

    await expect.element(page.getByRole("tab", { name: "Loop health", selected: true })).toBeVisible();
    await expect.element(page.getByRole("slider", { name: "Closed loop by hour" })).toBeVisible();
    expect(calls.query).toHaveLength(0);
    expect(calls.loopHealth.at(-1)).toEqual({
      startDate: "2026-10-09",
      endDate: "2026-10-22",
      days: [6],
      excludeIllness: false,
    });
  });

  it("drops a filter Loop health says the viewer cannot use", async () => {
    healthResponse = loopHealthResponse({ scopes: ["glucose.read", "treatments.read", "therapy.read", "reports.read"] });
    open("?tab=loop-health&excludeOpenLoop=true");

    await expect.poll(() => calls.goto.length).toBeGreaterThan(0);
    const search = new URL(calls.goto.at(-1)![0], "http://localhost").searchParams;
    expect(search.has("excludeOpenLoop")).toBe(false);
    expect(search.get("tab")).toBe("loop-health");
  });

  it("switches to Loop health through the URL", async () => {
    open("");
    await page.getByRole("tab", { name: "Loop health" }).click();

    const search = new URL(calls.goto.at(-1)![0], "http://localhost").searchParams;
    expect(search.get("tab")).toBe("loop-health");
  });
});
