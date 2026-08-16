/**
 * Remote function to get the current tenant ID.
 * Shared across settings pages that need the current tenant context.
 */
import { getRequestEvent, query } from "$app/server";
import { error, redirect } from "@sveltejs/kit";
import { activeTenants } from "$lib/utils/tenant-host";

/**
 * Get the current tenant ID for the authenticated user.
 * Returns the first tenant the user is a member of and can reach.
 */
export const getCurrentTenantId = query(async () => {
  const { locals, url } = getRequestEvent();

  if (!locals.isAuthenticated) {
    throw redirect(302, `/auth/login?returnUrl=${encodeURIComponent(url.pathname + url.search)}`);
  }

  const apiClient = locals.apiClient;
  try {
    const tenants = await apiClient.myTenants.getMyTenants();
    return activeTenants(tenants)[0]?.id ?? null;
  } catch (err) {
    const status = (err as any)?.status;
    if (status === 401) {
      throw redirect(302, `/auth/login?returnUrl=${encodeURIComponent(url.pathname + url.search)}`);
    }
    if (status === 403) throw error(403, "Forbidden");
    console.error("Error in getCurrentTenantId:", err);
    throw error(500, "Failed to get current tenant");
  }
});
