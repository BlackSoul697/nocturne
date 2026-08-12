import type { LayoutServerLoad } from "./$types";
import { extractTenantSlug, getOriginalHost, isShareHost } from "$lib/server/request-host";

/**
 * The viewer's granted scopes, which the UI uses to offer only what the viewer can load.
 * `authHandle` resolves them for a signed-in member; a public share link and a guest link
 * never reach that branch, so their grant — the share's shareable read categories, or the
 * scopes on the guest grant — is resolved here instead. Failure leaves the viewer with
 * nothing rather than an over-offer.
 */
async function resolveEffectivePermissions(
  locals: App.Locals,
  host: string | null,
): Promise<string[]> {
  if (locals.effectivePermissions) return locals.effectivePermissions;
  if (!isShareHost(host) && !locals.isGuestSession) return [];

  try {
    return await locals.apiClient.myPermissions.getMyPermissions();
  } catch {
    return [];
  }
}

/**
 * Root layout server load function.
 * Provides session data to all routes.
 * Auth gating is handled by route group layouts.
 * Setup/recovery mode detection is in hooks.server.ts.
 */
export const load: LayoutServerLoad = async ({ locals, request }) => {
  // Tenant identity is resolved here, from the request host against BASE_DOMAIN,
  // so the browser never has to guess it by counting hostname labels. A share
  // host carries a token rather than a slug, so it has no tenant to name.
  const host = getOriginalHost(request);
  const baseDomain = process.env.BASE_DOMAIN ?? null;
  const tenantSlug = isShareHost(host) ? null : extractTenantSlug(host, baseDomain);

  return {
    user: locals.user,
    isAuthenticated: locals.isAuthenticated,
    effectivePermissions: await resolveEffectivePermissions(locals, host),
    isPlatformAdmin: locals.isPlatformAdmin,
    isPlatformAccessGrant: locals.isPlatformAccessGrant ?? false,
    tenantSlug,
    baseDomain,
  };
};
