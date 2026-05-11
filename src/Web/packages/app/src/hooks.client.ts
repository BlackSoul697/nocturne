import type { HandleClientError } from "@sveltejs/kit";

/**
 * Per-tab session id used to correlate multiple client errors from the same
 * browser session in the OTel backend. Stored in sessionStorage so it survives
 * SPA navigation but resets when the tab is closed. Falls back to a fresh
 * id if sessionStorage is unavailable (private browsing, SSR).
 */
function getOrCreateSessionId(): string {
  try {
    const KEY = "nocturne-session-id";
    const existing = sessionStorage.getItem(KEY);
    if (existing) return existing;
    const fresh = crypto.randomUUID();
    sessionStorage.setItem(KEY, fresh);
    return fresh;
  } catch {
    return crypto.randomUUID();
  }
}

export const handleError: HandleClientError = ({ error, event }) => {
  const errorId = crypto.randomUUID();

  const message =
    error instanceof Error
      ? error.message
      : typeof error === "string"
        ? error
        : "An unexpected error occurred";

  const stack = error instanceof Error ? error.stack : undefined;
  const errorName = error instanceof Error ? error.name : undefined;

  console.error(`Error ID: ${errorId}`, error);

  // Fire-and-forget — do not await, do not retry
  fetch("/api/otel/errors", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      errorId,
      message,
      stack,
      errorName,
      url: window.location.href,
      route: event?.url?.pathname ?? window.location.pathname,
      userAgent: navigator.userAgent,
      sessionId: getOrCreateSessionId(),
      locale: typeof navigator !== "undefined" ? navigator.language : undefined,
      viewport: `${window.innerWidth}x${window.innerHeight}`,
      timestamp: new Date().toISOString(),
    }),
  }).catch(() => {
    // Swallow — reporting failure should never mask the original error
  });

  return { message, errorId };
};
