/**
 * Pure route-classification and auth-redirect logic shared by the server hooks.
 *
 * This module deliberately has NO SvelteKit/runtime imports ($env, $app, the
 * api client, …) so it can be unit-tested in a plain Node environment and so
 * the security-critical decision logic lives in one auditable place.
 */

/** Static asset paths that bypass all middleware. */
export const STATIC_ASSET_PREFIXES = ["/_app", "/assets", "/favicon.ico"] as const;

/** Route prefixes that bypass requireAuthentication enforcement. */
export const PUBLIC_PREFIXES = [
  "/auth",
  "/api",
  "/setup",
  "/clock",
  "/invite",
  "/terms",
  "/privacy",
  "/guest",
] as const;

/**
 * Whether a path bypasses requireAuthentication enforcement.
 *
 * Note that "/" is intentionally NOT public — the dashboard root is a protected
 * route, so an unauthenticated visitor to "/" is redirected to login when the
 * instance requires authentication. Public, read-only instances pass through
 * because their requireAuthentication flag is false (see shouldRedirectToLogin).
 */
export function isPublicRoute(pathname: string): boolean {
  return (
    PUBLIC_PREFIXES.some((p) => pathname.startsWith(p)) ||
    STATIC_ASSET_PREFIXES.some((p) => pathname.startsWith(p))
  );
}

/**
 * Decide whether an incoming request should be redirected to the login page,
 * returning the Location value (with returnUrl) or null to allow through.
 *
 * A redirect fires only when all of the following hold:
 *  - the path is not a public route,
 *  - the instance requires authentication, and
 *  - the request is not already authenticated.
 *
 * `requireAuthentication` is intentionally typed to allow undefined so callers
 * can pass the raw locals value; only an explicit truthy value gates access.
 */
export function shouldRedirectToLogin(args: {
  pathname: string;
  search: string;
  requireAuthentication: boolean | undefined;
  isAuthenticated: boolean;
}): string | null {
  const { pathname, search, requireAuthentication, isAuthenticated } = args;
  if (isPublicRoute(pathname) || !requireAuthentication || isAuthenticated) {
    return null;
  }
  const returnUrl = encodeURIComponent(pathname + search);
  return `/auth/login?returnUrl=${returnUrl}`;
}
