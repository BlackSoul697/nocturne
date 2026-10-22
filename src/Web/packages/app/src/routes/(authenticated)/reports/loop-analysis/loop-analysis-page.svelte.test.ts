import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { page as pageState } from "$app/state";
import {
  EventAnchorKind,
  type DailyProfileResponse,
  type EventResponseReport,
  type LoopHealthResponse,
  type PredictionAccuracyResponse,
  type SettingsEvidenceReport,
} from "$lib/api";
import { remoteQuery } from "$lib/test-stubs/remote-resource";
import {
  ALL_SCOPES,
  profileResponse,
} from "$lib/components/reports/loop-analysis/daily-profile.test-fixtures";
import { eventResponse } from "$lib/components/reports/loop-analysis/event-response.test-fixtures";
import { predictionResponse } from "$lib/components/reports/loop-analysis/prediction-accuracy.test-fixtures";
import { loopHealthResponse } from "$lib/components/reports/loop-analysis/loop-health.test-fixtures";
import { settingsEvidenceResponse } from "$lib/components/reports/loop-analysis/settings-evidence.test-fixtures";

type Registration = { current: () => unknown; active: () => boolean };

const calls = vi.hoisted(() => ({
  query: [] as unknown[],
  events: [] as unknown[],
  predictions: [] as unknown[],
  loopHealth: [] as unknown[],
  settingsEvidence: [] as unknown[],
  goto: [] as [string, unknown][],
  /** Every resource the page created, and whether it reports to the guard. */
  resources: [] as Registration[],
  /** Whether a goto moves the page's URL, as SvelteKit's does, rather than only being recorded. */
  navigate: false,
}));

// The page reads `page.url` reactively; the shared stub's is a plain property.
vi.mock("$app/state", async () => {
  const stub = await vi.importActual<typeof import("$app/state")>("$app/state");
  const { reactiveFields } = await import("$lib/test-stubs/reactive-fields.svelte");
  return { ...stub, page: reactiveFields({ ...stub.page }) };
});
let response: DailyProfileResponse;
let events: EventResponseReport;
let predictions: PredictionAccuracyResponse | undefined;
let health: LoopHealthResponse;
let evidence: SettingsEvidenceReport | undefined;
/** `failing`: the one response whose request failed, when only one did. */
let resourceState: { stale?: boolean; error?: unknown; failing?: unknown } = {};

vi.mock("$api/generated/loopAnalysis.generated.remote", () => ({
  getDailyProfile: (input: unknown) => {
    calls.query.push(input);
    return remoteQuery(() => response);
  },
  getEventResponse: (input: unknown) => {
    calls.events.push(input);
    return remoteQuery(() => events);
  },
  getPredictionAccuracy: (input: unknown) => {
    calls.predictions.push(input);
    return remoteQuery(() => predictions);
  },
  getLoopHealth: (input: unknown) => {
    calls.loopHealth.push(input);
    return remoteQuery(() => health);
  },
  getSettingsEvidence: (input: unknown) => {
    calls.settingsEvidence.push(input);
    return remoteQuery(() => evidence);
  },
}));

vi.mock("$app/navigation", async () => {
  const { page: state } = await import("$app/state");
  return {
    goto: (url: string, options?: unknown) => {
      calls.goto.push([url, options]);
      if (calls.navigate) state.url = new URL(url, "http://localhost") as typeof state.url;
      return Promise.resolve();
    },
  };
});

vi.mock("$lib/hooks/date-params.svelte", () => ({
  requireDateParamsContext: () => ({ fromDay: "2026-10-09", toDay: "2026-10-22" }),
}));

vi.mock("$lib/hooks/resource-context.svelte", () => ({
  contextResource: <T>(create: () => { current: T | undefined }, options?: { active?: () => boolean }) => {
    const current = () => create().current;
    calls.resources.push({ current, active: options?.active ?? (() => true) });
    const failed = () => resourceState.error ?? (resourceState.failing !== undefined && resourceState.failing === current() ? new Error("Request rejected") : null);
    return {
      get current() {
        return current();
      },
      get stale() {
        return resourceState.stale ?? false;
      },
      get error() {
        return failed();
      },
    };
  },
}));

import LoopAnalysisPage from "./+page.svelte";

function open(search: string, data: Record<string, unknown> = {}) {
  pageState.url = new URL(`http://localhost/reports/loop-analysis${search}`) as typeof pageState.url;
  for (const key of Object.keys(pageState.data)) delete pageState.data[key];
  Object.assign(pageState.data, { effectivePermissions: ALL_SCOPES, ...data });
  return render(LoopAnalysisPage);
}

const lastQuery = () => calls.query.at(-1);
const searchOf = (url: string) => new URL(url, "http://localhost").searchParams;

describe("Loop Analysis page", () => {
  beforeEach(() => {
    calls.query.length = 0;
    calls.events.length = 0;
    calls.predictions.length = 0;
    calls.loopHealth.length = 0;
    calls.settingsEvidence.length = 0;
    calls.resources.length = 0;
    calls.navigate = false;
    calls.goto.length = 0;
    response = profileResponse();
    events = eventResponse();
    predictions = predictionResponse();
    health = loopHealthResponse();
    evidence = settingsEvidenceResponse();
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
    const search = searchOf(target![0]);
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
    const search = searchOf(url);
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

  it("lists all five tabs", async () => {
    open("");

    await expect.element(page.getByRole("tab", { name: "Daily profile", selected: true })).toBeVisible();
    for (const name of ["Meals & events", "Predictions", "Loop health", "Settings evidence"])
      await expect.element(page.getByRole("tab", { name })).toBeVisible();
  });

  it("fetches only the Meals & events tab when it is on screen, with its cohorts", async () => {
    open("?tab=meals-events&anchor1=Meal&fromMinute1=300&toMinute1=600&anchor2=UnannouncedRise&minutesAfter=480");

    await expect.element(page.getByRole("slider", { name: "Glucose change from the event" })).toBeVisible();
    expect(calls.query).toHaveLength(0);
    expect(calls.events.at(-1)).toEqual({
      startDate: "2026-10-09",
      endDate: "2026-10-22",
      anchor1: EventAnchorKind.Meal,
      fromMinute1: 300,
      toMinute1: 600,
      anchor2: EventAnchorKind.UnannouncedRise,
      minutesAfter: 480,
    });
    await expect.element(page.getByLabelText("Cohort 1 from")).toHaveValue("05:00");
  });

  it("writes a builder change into the URL beside the shared filter", async () => {
    open("?tab=meals-events&days=14&excludeExercise=true");

    await page.getByLabelText("Isolated events only").click();
    const search = searchOf(calls.goto.at(-1)![0]);
    expect(search.get("isolatedOnly")).toBe("true");
    expect(search.get("anchor1")).toBe("Meal");
    expect(search.get("anchor2")).toBe("UnannouncedRise");
    expect(search.get("excludeExercise")).toBe("true");
    expect(search.get("tab")).toBe("meals-events");
  });

  it("drops from the URL a cohort whose events the viewer cannot read", async () => {
    events = {
      ...eventResponse(),
      unavailableAnchors: [{ anchor: EventAnchorKind.Meal, requiredScope: "treatments.read" }],
    };
    open("?tab=meals-events&anchor1=Meal&anchor2=SensorStart");

    await expect.poll(() => calls.goto.length).toBeGreaterThan(0);
    const search = searchOf(calls.goto.at(-1)![0]);
    expect(search.get("anchor1")).toBe("SensorStart");
    expect(search.has("anchor2")).toBe(false);
  });

  it("waits for a loading tab's echo before the bar settles", async () => {
    resourceState = { stale: true };
    open("?tab=meals-events&days=14&anchor1=Meal");

    await expect.element(page.getByRole("group", { name: "Exclude" })).toHaveAttribute("aria-busy", "true");
    expect(calls.query).toHaveLength(0);
  });

  it("fetches only the Predictions tab when it is on screen, with the shared filter", async () => {
    open("?tab=predictions&compare=previous&excludeExercise=true");

    await expect.element(page.getByRole("slider", { name: "UAM", exact: true })).toBeVisible();
    expect(calls.query).toHaveLength(0);
    expect(calls.predictions.at(-1)).toEqual({
      startDate: "2026-10-09",
      endDate: "2026-10-22",
      compare: "Previous",
      excludeExercise: true,
    });
  });

  it("drops from the URL a filter the Predictions tab's viewer cannot use", async () => {
    const scopes = ["glucose.read", "treatments.read", "therapy.read", "reports.read"];
    predictions = predictionResponse({ scopes });
    open("?tab=predictions&excludeOpenLoop=true");

    await expect.element(page.getByText("Prediction accuracy is hidden")).toBeVisible();
    await expect.poll(() => calls.goto.length).toBeGreaterThan(0);
    const search = searchOf(calls.goto.at(-1)![0]);
    expect(search.has("excludeOpenLoop")).toBe(false);
    expect(search.get("tab")).toBe("predictions");
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

  it("fetches only Settings evidence on its tab, with the same filter", async () => {
    open("?tab=settings-evidence&weekdays=6&excludeIllness=false");

    await expect.element(page.getByRole("tab", { name: "Settings evidence", selected: true })).toBeVisible();
    await expect.element(page.getByRole("slider", { name: "Basal by time of day" })).toBeVisible();
    expect(calls.query).toHaveLength(0);
    expect(calls.loopHealth).toHaveLength(0);
    expect(calls.settingsEvidence.at(-1)).toEqual({
      startDate: "2026-10-09",
      endDate: "2026-10-22",
      days: [6],
      excludeIllness: false,
    });
  });

  it("links Settings evidence's ISF to Loop health's sensitivity ratio under the same filter", async () => {
    open("?tab=settings-evidence&weekdays=6&excludeIllness=false");

    const link = page.getByRole("link", { name: /sensitivity ratio by hour, in Loop health/ });
    await expect.element(link).toBeVisible();
    const href = new URL(link.element().getAttribute("href")!, "http://localhost");
    expect(href.hash).toBe("#sensitivity");
    expect(href.searchParams.get("tab")).toBe("loop-health");
    expect(href.searchParams.get("weekdays")).toBe("6");
    expect(href.searchParams.get("excludeIllness")).toBe("false");
  });

  it("drops a filter Loop health says the viewer cannot use", async () => {
    health = loopHealthResponse({ scopes: ["glucose.read", "treatments.read", "therapy.read", "reports.read"] });
    open("?tab=loop-health&excludeOpenLoop=true");

    await expect.poll(() => calls.goto.length).toBeGreaterThan(0);
    const search = searchOf(calls.goto.at(-1)![0]);
    expect(search.has("excludeOpenLoop")).toBe(false);
    expect(search.get("tab")).toBe("loop-health");
  });

  it("offers the profiles of a tab opened straight from its URL", async () => {
    predictions = { ...predictionResponse(), profileNames: ["Default", "Weekend"] };
    open("?tab=predictions");

    await expect.element(page.getByRole("slider", { name: "UAM", exact: true })).toBeVisible();
    expect(calls.query).toHaveLength(0);
    await page.elementLocator(document.getElementById("loop-analysis-profile")!).click();
    await expect.element(page.getByRole("option", { name: "Weekend" })).toBeVisible();
  });

  it("keeps a tab's response across a switch away and back, and never fetches a tab off screen", async () => {
    calls.navigate = true;
    const distinct = (list: unknown[]) => new Set(list.map((q) => JSON.stringify(q)));
    open("?tab=predictions");

    await expect.element(page.getByRole("slider", { name: "UAM", exact: true })).toBeVisible();
    expect(calls.query).toHaveLength(0);

    await page.getByRole("tab", { name: "Daily profile" }).click();
    await expect.element(page.getByRole("slider", { name: "Glucose", exact: true })).toBeVisible();
    await expect.element(page.getByRole("group", { name: "Exclude" })).toHaveAttribute("aria-busy", "false");

    // A filter changed off the Predictions tab is not fetched for it until it is back on screen.
    await page.getByLabelText("Exercise").click();
    expect(distinct(calls.predictions)).toEqual(new Set([JSON.stringify({ startDate: "2026-10-09", endDate: "2026-10-22" })]));

    await page.getByRole("tab", { name: "Predictions" }).click();
    await expect.element(page.getByRole("slider", { name: "UAM", exact: true })).toBeVisible();
    await expect.element(page.getByRole("group", { name: "Exclude" })).toHaveAttribute("aria-busy", "false");
    expect(calls.predictions.at(-1)).toEqual({ startDate: "2026-10-09", endDate: "2026-10-22", excludeExercise: true });

    // Back and forth with the filter unchanged asks for nothing new.
    const asked = distinct(calls.predictions).size;
    await page.getByRole("tab", { name: "Daily profile" }).click();
    await page.getByRole("tab", { name: "Predictions" }).click();
    await expect.element(page.getByRole("slider", { name: "UAM", exact: true })).toBeVisible();
    expect(distinct(calls.predictions).size).toBe(asked);
  });

  it("loads a tab's first response inside the tab, leaving the filter bar and the tabs in place", async () => {
    predictions = undefined;
    open("?tab=predictions");

    await expect.element(page.getByRole("status", { name: "Loading" })).toBeVisible();
    await expect.element(page.getByRole("tab", { name: "Predictions", selected: true })).toBeVisible();
    await expect.element(page.getByLabelText("Exercise")).toBeVisible();
    expect(calls.resources.filter((r) => r.active())).toHaveLength(0);
  });

  it("loads Settings evidence's first response inside its tab", async () => {
    evidence = undefined;
    open("?tab=settings-evidence");

    await expect.element(page.getByRole("status", { name: "Loading" })).toBeVisible();
    await expect.element(page.getByRole("tab", { name: "Settings evidence", selected: true })).toBeVisible();
  });

  it("keeps the tab on screen when a tab left behind failed, and only it reports to the guard", async () => {
    calls.navigate = true;
    resourceState = { failing: predictions };
    open("?tab=predictions");
    await expect.poll(() => calls.resources.length).toBeGreaterThan(0);

    await page.getByRole("tab", { name: "Daily profile" }).click();
    await expect.element(page.getByRole("slider", { name: "Glucose", exact: true })).toBeVisible();
    await expect.element(page.getByRole("group", { name: "Exclude" })).toHaveAttribute("aria-busy", "false");

    const reporting = calls.resources.filter((r) => r.active()).map((r) => r.current());
    expect(reporting).toEqual([response]);
  });
});
