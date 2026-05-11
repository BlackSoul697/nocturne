import type { RequestHandler } from "./$types";
import { trace, SpanStatusCode } from "@opentelemetry/api";
import { randomUUID } from "crypto";

const tracer = trace.getTracer("nocturne-web-client", "1.0.0");

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const MAX_FIELD_LENGTH = 4096;
const SHORT_FIELD_LENGTH = 256;

function clip(value: unknown, max: number): string {
  if (typeof value !== "string") return "";
  return value.slice(0, max);
}

export const POST: RequestHandler = async ({ request, locals }) => {
  const contentLength = parseInt(request.headers.get("content-length") ?? "0", 10);
  if (contentLength > 16_384) {
    return new Response(null, { status: 413 });
  }

  let body: {
    errorId?: string;
    message?: string;
    stack?: string;
    errorName?: string;
    url?: string;
    route?: string;
    userAgent?: string;
    sessionId?: string;
    locale?: string;
    viewport?: string;
    timestamp?: string;
  };

  try {
    body = await request.json();
  } catch {
    return new Response(JSON.stringify({ error: "Invalid JSON" }), {
      status: 400,
      headers: { "Content-Type": "application/json" },
    });
  }

  const errorId =
    body.errorId && UUID_RE.test(body.errorId) ? body.errorId : randomUUID();
  const sessionId =
    body.sessionId && UUID_RE.test(body.sessionId) ? body.sessionId : "";

  const span = tracer.startSpan("client-error", {
    attributes: {
      "error.id": errorId,
      "error.name": clip(body.errorName, SHORT_FIELD_LENGTH),
      "error.message": clip(body.message, MAX_FIELD_LENGTH),
      "error.stack": clip(body.stack, MAX_FIELD_LENGTH),
      "error.url": clip(body.url, SHORT_FIELD_LENGTH),
      "http.route": clip(body.route, SHORT_FIELD_LENGTH),
      "http.user_agent":
        clip(body.userAgent, SHORT_FIELD_LENGTH) ||
        request.headers.get("user-agent") ||
        "",
      "session.id": sessionId,
      "client.locale": clip(body.locale, 16),
      "client.viewport": clip(body.viewport, 32),
      "client.timestamp": clip(body.timestamp, 64),
      // Stamp the authenticated user when available so client errors can be
      // joined to backend traces by subject.id in the OTel backend.
      "nocturne.subject_id": locals.user?.subjectId ?? "",
      "nocturne.is_guest_session": locals.isGuestSession === true,
    },
  });

  span.setStatus({
    code: SpanStatusCode.ERROR,
    message: clip(body.message, SHORT_FIELD_LENGTH),
  });
  span.end();

  return new Response(null, { status: 204 });
};
