/**
 * The public glucose clock is the one page meant to be framed: the desktop companion hosts
 * `/clock/{id}?embed=1` in an iframe of its own window.
 */
const FRAMEABLE_PREFIXES = ["/clock/"];

/**
 * Response headers every reply from the web app carries, including the /api responses it proxies.
 *
 * A public share host sends no referrer at all, keeping the token-bearing URL out of Referer
 * headers, and asks not to be indexed.
 */
export function applySecurityHeaders(
  headers: Headers,
  { isShareHost, pathname }: { isShareHost: boolean; pathname: string },
): void {
  headers.set("X-Content-Type-Options", "nosniff");

  if (isShareHost) {
    headers.set("Referrer-Policy", "no-referrer");
    headers.set("X-Robots-Tag", "noindex, nofollow");
  } else if (!headers.has("Referrer-Policy")) {
    headers.set("Referrer-Policy", "strict-origin-when-cross-origin");
  }

  if (!FRAMEABLE_PREFIXES.some((prefix) => pathname.startsWith(prefix))) {
    headers.set("X-Frame-Options", "DENY");
  }
}
