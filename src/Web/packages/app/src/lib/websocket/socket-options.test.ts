import { describe, it, expect, afterEach } from "vitest";
import { createServer, type Server } from "node:http";
import { io, type Socket } from "socket.io-client";
import { realtimeSocketOptions } from "./socket-options";
import type { WebSocketConfig } from "./types";

const config: WebSocketConfig = {
  url: "",
  reconnectAttempts: Infinity,
  reconnectDelay: 50,
  maxReconnectDelay: 100,
  pingTimeout: 5000,
  pingInterval: 2000,
};

let server: Server | null = null;
let socket: Socket | null = null;

afterEach(async () => {
  socket?.close();
  socket = null;
  if (server) {
    const s = server;
    server = null;
    await new Promise<void>((resolve) => s.close(() => resolve()));
  }
});

/** A server that answers HTTP but refuses the WebSocket upgrade, standing in for
 *  the proxies, carriers and extensions that do the same to real users. */
async function startWebsocketHostileServer(): Promise<string> {
  server = createServer((_req, res) => {
    res.writeHead(502);
    res.end();
  });
  server.on("upgrade", (_req, socketConn) => socketConn.destroy());
  await new Promise<void>((resolve) => server!.listen(0, "127.0.0.1", resolve));
  const address = server.address();
  if (typeof address === "string" || address === null) {
    throw new Error("expected a TCP address");
  }
  return `http://127.0.0.1:${address.port}`;
}

async function observeAttemptedTransports(
  socketUnderTest: Socket,
  durationMs: number
): Promise<Set<string>> {
  const seen = new Set<string>();
  const deadline = Date.now() + durationMs;
  while (Date.now() < deadline) {
    const name = socketUnderTest.io.engine?.transport?.name;
    if (name) seen.add(name);
    await new Promise((resolve) => setTimeout(resolve, 5));
  }
  return seen;
}

describe("realtimeSocketOptions", () => {
  it("falls back to polling when the WebSocket transport fails", async () => {
    const url = await startWebsocketHostileServer();

    socket = io(url, realtimeSocketOptions(config, (cb) => cb({ token: "" })));
    const attempted = await observeAttemptedTransports(socket, 1500);

    // Without tryAllTransports, engine.io-client abandons the attempt on the
    // first transport error and "polling" is never reached.
    expect(attempted).toContain("websocket");
    expect(attempted).toContain("polling");
  });

  it("keeps WebSocket as the preferred transport", () => {
    const options = realtimeSocketOptions(config, (cb) => cb({}));

    expect(options.transports[0]).toBe("websocket");
    expect(options.tryAllTransports).toBe(true);
  });

  it("carries the caller's reconnection budget through", () => {
    const options = realtimeSocketOptions(config, (cb) => cb({}));

    expect(options.reconnection).toBe(true);
    expect(options.reconnectionAttempts).toBe(config.reconnectAttempts);
    expect(options.reconnectionDelay).toBe(config.reconnectDelay);
    expect(options.reconnectionDelayMax).toBe(config.maxReconnectDelay);
  });
});
