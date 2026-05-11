import { redirect } from "@sveltejs/kit";
import type { LayoutServerLoad } from "./$types";
import {
  getApiBaseUrl,
  getHashedInstanceKey,
  createServerApiClient,
} from "$lib/server/api-client-factory";
import { AUTH_COOKIE_NAMES } from "$lib/config/auth-cookies";
import type { SensorGlucose, TenantDto } from "$lib/api/generated/nocturne-api-client";

const LIVE_PAGES = ["/roster", "/attention"];

export const load: LayoutServerLoad = async (event) => {
  if (!event.locals.isPlatformView) {
    throw redirect(303, "/dashboard");
  }

  if (!event.locals.isAuthenticated || !event.locals.user) {
    throw redirect(303, `/auth/login?returnUrl=${encodeURIComponent(event.url.pathname)}`);
  }

  const apiBaseUrl = getApiBaseUrl();
  if (!apiBaseUrl) throw new Error("NOCTURNE_API_URL is not configured");
  const isLivePage = LIVE_PAGES.some((p) => event.url.pathname.startsWith(p));

  const tenants = event.locals.isPlatformAdmin
    ? await event.locals.apiClient.tenant.getAll()
    : await event.locals.apiClient.myTenants.getMyTenants();

  if (!isLivePage) {
    return {
      user: event.locals.user,
      apexHost: event.locals.apexHost ?? "",
      isPlatformAdmin: event.locals.isPlatformAdmin,
      tenants: tenants ?? [],
      snapshots: [],
    };
  }

  event.depends("app:roster-snapshots");

  const accessToken = event.cookies.get(AUTH_COOKIE_NAMES.accessToken);
  const refreshToken = event.cookies.get(AUTH_COOKIE_NAMES.refreshToken);
  const apexHost = event.locals.apexHost ?? "";
  const hashedKey = getHashedInstanceKey();

  const snapshots = await Promise.all(
    (tenants ?? []).map(async (tenant) => {
      try {
        const client = createServerApiClient(apiBaseUrl, event.fetch, {
          accessToken,
          refreshToken,
          hashedInstanceKey: hashedKey,
          extraHeaders: {
            "X-Forwarded-Host": `${tenant.slug}.${apexHost}`,
            "X-Forwarded-Proto": "https",
          },
        });
        const result = await client.sensorGlucose.getAll(
          undefined,
          undefined,
          36,
          0,
          "timestamp_desc",
        );
        return { tenant, readings: result?.data ?? [] };
      } catch {
        return { tenant, readings: [] as SensorGlucose[] };
      }
    }),
  );

  return {
    user: event.locals.user,
    apexHost,
    isPlatformAdmin: event.locals.isPlatformAdmin,
    tenants: tenants ?? [],
    snapshots,
  };
};
