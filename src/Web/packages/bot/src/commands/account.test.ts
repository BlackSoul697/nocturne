import { describe, it, expect, vi } from "vitest";
import type { Chat, SlashCommandEvent } from "chat";
import { registerAccountCommands } from "./account.js";
import { runWithContext, type BotRequestContext } from "../lib/request-context.js";
import type { BotApiClient } from "../types.js";

vi.mock("../lib/logger.js", () => ({
  createLogger: () => ({ info: vi.fn(), warn: vi.fn(), error: vi.fn(), debug: vi.fn() }),
}));

function connectHandler() {
  const commands = new Map<string, (event: SlashCommandEvent) => Promise<void>>();
  const bot = {
    onSlashCommand: (name: string, fn: (event: SlashCommandEvent) => Promise<void>) => {
      commands.set(name, fn);
    },
  } as unknown as Chat;
  registerAccountCommands(bot, "nocturne.test");
  return commands.get("/connect")!;
}

describe("/connect", () => {
  it("records the chat account's username so the confirm page can name it", async () => {
    const create = vi.fn().mockResolvedValue({ token: "pending-token" });
    const context: BotRequestContext = {
      unscopedApi: { pendingLinks: { create } } as unknown as BotApiClient,
      scopedApiFactory: vi.fn(),
      resolvedTenantSlug: null,
      resolvedLink: null,
    };
    const event = {
      adapter: { name: "telegram" },
      channel: { post: vi.fn(), postEphemeral: vi.fn().mockResolvedValue(null) },
      text: "sleepy",
      user: { userId: "4242", userName: "sam_t", fullName: "Sam Tester" },
    } as unknown as SlashCommandEvent;

    await runWithContext(context, () => connectHandler()(event));

    expect(create).toHaveBeenCalledExactlyOnceWith("telegram", "4242", "sam_t", "sleepy", "connect-slash");
  });
});
