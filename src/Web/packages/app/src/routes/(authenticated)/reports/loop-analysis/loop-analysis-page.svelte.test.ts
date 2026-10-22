import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { page as pageState } from "$app/state";
import type { DailyProfileResponse } from "$lib/api";
import { remoteQuery } from "$lib/test-stubs/remote-resource";
import {
  ALL_SCOPES,
  profileResponse,
} from "$lib/components/reports/loop-analysis/daily-profile.test-fixtures";

const calls = vi.hoisted(() => ({ query: [] as unknown[], goto: [] as [string, unknown][] }));
let response: DailyProfileResponse;

vi.mock("$api/generated/loopAnalysis.generated.remote", () => ({
  getDailyProfile: (input: unknown) => {
    calls.query.push(input);
    return remoteQuery(() => response);
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
    calls.goto.length = 0;
    response = profileResponse();
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
});
