import { describe, expect, it, vi } from "vitest";
import { isRedirect, type Redirect } from "@sveltejs/kit";

vi.mock("$app/paths", () => ({ resolve: (path: string) => path }));

const { load } = await import("./+page");

async function redirectOf(search: string): Promise<Redirect> {
  try {
    await load({ url: new URL(`http://localhost/reports/loop-analysis${search}`) } as Parameters<typeof load>[0]);
  } catch (error) {
    if (isRedirect(error)) return error;
    throw error;
  }
  throw new Error("load did not redirect");
}

describe("the one-page Loop Analysis URL", () => {
  it("opens Daily profile with its query string", async () => {
    const target = await redirectOf("?days=30&weekdays=6&excludeIllness=false");
    expect(target.status).toBe(307);
    expect(target.location).toBe("/reports/loop-analysis/daily-profile?days=30&weekdays=6&excludeIllness=false");
  });

  it.each([
    ["meals-events", "meals-and-events"],
    ["predictions", "prediction-accuracy"],
    ["loop-health", "loop-health"],
    ["settings-evidence", "settings-evidence"],
    ["daily-profile", "daily-profile"],
    ["nope", "daily-profile"],
  ])("opens the page the %s tab showed, without the tab", async (tab, target) => {
    expect((await redirectOf(`?tab=${tab}&days=14`)).location).toBe(`/reports/loop-analysis/${target}?days=14`);
  });

  it("opens Daily profile from a bare URL", async () => {
    expect((await redirectOf("")).location).toBe("/reports/loop-analysis/daily-profile");
  });
});
