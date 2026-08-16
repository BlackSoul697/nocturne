/**
 * Tenant addressing: which tenants a signed-in visitor can go to, and the URLs that take them there.
 *
 * Tenants are addressed as subdomains of a shared base domain
 * (`{slug}.{base-domain}`). The base domain itself comes from the server
 * (BASE_DOMAIN, via the root layout) and already carries any non-default port,
 * so it must never be re-derived or re-decorated on the client.
 */

/** Build the root URL for a tenant subdomain. */
export function tenantUrl(
  slug: string,
  baseDomain: string,
  protocol: string = typeof window !== "undefined"
    ? window.location.protocol
    : "https:"
): string {
  return `${protocol}//${slug}.${baseDomain}/`;
}

/** A membership as the tenant list returns it: every field of the generated TenantDto is optional. */
export interface TenantListEntry {
  id?: string | null;
  slug?: string | null;
  displayName?: string | null;
  isActive?: boolean | null;
}

/**
 * The tenants a signed-in visitor can actually reach.
 *
 * An inactive tenant's host answers 403 on every path, so it is never somewhere to send anyone —
 * neither by redirect nor by a switcher entry — and it never counts towards "how many tenants does
 * this user have". An absent flag means active: the generated DTO marks every property optional,
 * and reading a missing field as inactive would strand a caregiver whose only tenant is fine.
 */
export function activeTenants<T extends { isActive?: boolean | null }>(
  tenants: readonly T[] | null | undefined
): T[] {
  return (tenants ?? []).filter((t) => t.isActive ?? true);
}

/**
 * Where a tenantless host should send a signed-in visitor.
 *
 * A caregiver with access to exactly one tenant has nothing to choose between, so the dashboard
 * would be a single tile in front of the app they actually want: send them straight to it.
 * Returns null — meaning "render the dashboard" — for zero or several tenants, or when no base
 * domain is configured and so no tenant URL can be built.
 *
 * Inactive tenants do not count and are never a destination. Their host answers 403 on every
 * path, which lands the visitor on a login page whose sign-in cannot succeed and which links
 * nowhere else, so a redirect there is a dead end only the URL bar can escape.
 *
 * It also returns null when the sole tenant's own slug is a reserved dashboard slug. Its host is
 * then the dashboard host, so redirecting there would land back on this same load and redirect
 * again, forever. Nothing is reserved by default, so this only arises once an operator sets
 * DASHBOARD_SLUGS — and it may name a slug some tenant already holds.
 */
export function resolveSingleTenantLanding(
  tenants: readonly TenantListEntry[] | null | undefined,
  baseDomain: string | null | undefined,
  protocol?: string,
  dashboardSlugs: readonly string[] = []
): string | null {
  if (!baseDomain) return null;

  const slugs = activeTenants(tenants)
    .map((t) => t.slug)
    .filter((s): s is string => !!s);
  if (slugs.length !== 1) return null;

  const slug = slugs[0]!;
  if (dashboardSlugs.includes(slug.toLowerCase())) return null;

  return tenantUrl(slug, baseDomain, protocol);
}

/** One entry of the sidebar tenant switcher: a tenant this host can be swapped for. */
export interface TenantSwitcherTarget {
  id: string;
  slug: string;
  displayName: string | null;
}

export interface TenantSwitcher {
  /** The tenants the switcher can navigate to, i.e. everything but the one already being viewed. */
  targets: TenantSwitcherTarget[];
  /** How many tenants this user has to move between; below two there is nothing to switch. */
  totalCount: number;
  /** The tenant the switcher calls "My Data" — the first one, whatever host it is read on. */
  defaultSlug: string | null;
}

/**
 * The sidebar tenant switcher for a visitor viewing `currentSlug` (null on a tenantless host).
 *
 * Only active tenants take part, in all three answers. A target must be reachable, and a count
 * that included unreachable tenants would offer a switcher — and a Tenants nav entry — to someone
 * with a single usable tenant and nowhere to switch to. On a tenantless host currentSlug is null,
 * so every tenant is a target and an unfiltered list would put the very destination the dashboard
 * refuses to redirect to one click away in the same sidebar.
 */
export function resolveTenantSwitcher(
  tenants: readonly TenantListEntry[] | null | undefined,
  currentSlug: string | null | undefined
): TenantSwitcher {
  const active = activeTenants(tenants);

  return {
    totalCount: active.length,
    defaultSlug: active[0]?.slug ?? null,
    targets: active
      .filter(
        (t): t is TenantListEntry & { id: string; slug: string } =>
          !!t.id && !!t.slug && t.slug !== currentSlug
      )
      .map((t) => ({
        id: t.id,
        slug: t.slug,
        displayName: t.displayName ?? null,
      })),
  };
}
