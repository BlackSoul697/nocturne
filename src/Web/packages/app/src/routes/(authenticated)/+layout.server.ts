import { redirect } from "@sveltejs/kit";
import type { LayoutServerLoad } from "./$types";
import { checkOnboarding } from "$lib/server/onboarding-check";

/** Permissions that grant read access to glucose data (mirrors API's CanRead + OAuth scopes). */
const GLUCOSE_READ_PERMISSIONS = [
  "*",
  "api:*",
  "api:*:read",
  "readable",
  "glucose.read",
  "glucose.readwrite",
  "health.read",
  "health.readwrite",
];

function hasGlucoseReadPermission(permissions: string[]): boolean {
  return permissions.some((p) => GLUCOSE_READ_PERMISSIONS.includes(p));
}

export const load: LayoutServerLoad = async ({ locals, cookies, url }) => {
  // Guest sessions bypass onboarding — the data owner's instance is already set up.
  if (!locals.isGuestSession) {
    // Check onboarding first — if the instance needs setup, redirect there
    // regardless of auth state. This covers fresh installs where no tenant
    // or credentials exist yet.
    const onboarding = await checkOnboarding(
      cookies,
      locals.apiClient,
      url.protocol === "https:",
    );
    if (!onboarding.isComplete) {
      throw redirect(303, "/setup");
    }
  }

  if (!locals.isAuthenticated || !locals.user) {
    if (locals.requireAuthentication) {
      const returnUrl = encodeURIComponent(url.pathname + url.search);
      throw redirect(303, `/auth/login?returnUrl=${returnUrl}`);
    }
  }

  // Enable realtime glucose data for:
  // - Public sites (requireAuthentication: false) — authDefaultRoles grants readable
  // - Authenticated users with glucose read permissions
  // The API enforces authorization on each endpoint as defense in depth.
  const canViewRealtimeData =
    !locals.requireAuthentication ||
    hasGlucoseReadPermission(locals.effectivePermissions ?? []);

  // Fetch demo status for the banner (non-blocking — default to non-demo on failure)
  let isDemo = false;
  let nextResetAt: string | null = null;
  try {
    const status = await locals.apiClient.status.getStatus();
    isDemo = status.isDemo ?? false;
    nextResetAt = status.nextResetAt ? status.nextResetAt.toISOString() : null;
  } catch {
    // Swallow — demo banner is non-critical
  }

  return {
    user: locals.user ?? null,
    isGuestSession: locals.isGuestSession ?? false,
    guestExpiresAt: locals.guestExpiresAt ?? null,
    canViewRealtimeData,
    isDemo,
    nextResetAt,
  };
};
