import { describe, expect, it } from "vitest";
import { applySecurityHeaders } from "./security-headers";

function apply(pathname: string, isShareHost = false, initial: HeadersInit = {}): Headers {
  const headers = new Headers(initial);
  applySecurityHeaders(headers, { isShareHost, pathname });
  return headers;
}

describe("applySecurityHeaders", () => {
  it("forbids content sniffing on every response", () => {
    expect(apply("/").get("X-Content-Type-Options")).toBe("nosniff");
    expect(apply("/api/v4/alert-sounds/x/stream").get("X-Content-Type-Options")).toBe("nosniff");
    expect(apply("/", true).get("X-Content-Type-Options")).toBe("nosniff");
  });

  it("forbids framing everywhere but the public clock", () => {
    expect(apply("/").get("X-Frame-Options")).toBe("DENY");
    expect(apply("/settings/alerts").get("X-Frame-Options")).toBe("DENY");
    expect(apply("/clockwork").get("X-Frame-Options")).toBe("DENY");
    expect(apply("/clock/abc123").has("X-Frame-Options")).toBe(false);
  });

  it("sends the origin only, cross-origin, off the share host", () => {
    expect(apply("/").get("Referrer-Policy")).toBe("strict-origin-when-cross-origin");
    expect(apply("/").has("X-Robots-Tag")).toBe(false);
  });

  it("keeps a referrer policy a proxied response already chose", () => {
    expect(apply("/api/x", false, { "Referrer-Policy": "no-referrer" }).get("Referrer-Policy"))
      .toBe("no-referrer");
  });

  it("sends no referrer and asks not to be indexed on the share host", () => {
    const headers = apply("/", true, { "Referrer-Policy": "unsafe-url" });
    expect(headers.get("Referrer-Policy")).toBe("no-referrer");
    expect(headers.get("X-Robots-Tag")).toBe("noindex, nofollow");
  });
});
