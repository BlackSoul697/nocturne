import type { SensorGlucose, TenantDto } from "$lib/api/generated/nocturne-api-client";

// Derived shape the roster page passes to cards.
// Built from TenantDto + SensorGlucose[] in the page's $derived.
export interface RosterItem {
  id: string;
  slug: string;
  displayName: string;
  /** Latest BG in mg/dL, or null if no reading */
  mgdl: number | null;
  /** mg/dL change over last 5 min (latest - prev), or null */
  delta: number | null;
  /** Age of latest reading in minutes, or null */
  ageMin: number | null;
  /** Last 36 mg/dL values oldest-first, for sparkline */
  sparklinePoints: number[];
  /** Derived glucose status */
  status: "very-low" | "low" | "tight" | "in-range" | "high" | "very-high" | "stale" | "no-data";
  /** STUB-BACKEND: TIR percentages */
  tir: { veryLow: number; low: number; inRange: number; high: number; veryHigh: number };
}

export const STATUS_COLOR: Record<RosterItem["status"], string> = {
  "very-low":  "var(--glucose-very-low)",
  "low":       "var(--glucose-low)",
  "tight":     "var(--glucose-tight-range)",
  "in-range":  "var(--glucose-in-range)",
  "high":      "var(--glucose-high)",
  "very-high": "var(--glucose-very-high)",
  "stale":     "var(--muted-foreground)",
  "no-data":   "var(--muted-foreground)",
};

export const STATUS_LABEL: Record<RosterItem["status"], string> = {
  "very-low":  "Very low",
  "low":       "Low",
  "tight":     "Tight",
  "in-range":  "In range",
  "high":      "High",
  "very-high": "Very high",
  "stale":     "Stale",
  "no-data":   "No data",
};

export function dirArrow(delta: number | null): string {
  if (delta == null) return "→";
  if (delta > 15)  return "↑";
  if (delta > 6)   return "↗";
  if (delta > -6)  return "→";
  if (delta > -15) return "↘";
  return "↓";
}

export function ageStr(min: number | null): string {
  if (min == null) return "—";
  if (min < 1)     return "now";
  if (min < 60)    return `${min}m`;
  return `${Math.floor(min / 60)}h ${min % 60}m`;
}

// Shape of a single tenant's snapshot from the layout server load
export interface TenantSnapshot {
  tenant: TenantDto;
  readings: SensorGlucose[];
}

/**
 * Derives RosterItem[] from raw tenant snapshots.
 *
 * STUB-BACKEND: Glucose thresholds are hardcoded here (55/70/140/180/250 mg/dL).
 * When GET /api/v4/platform/roster-snapshots is implemented, it should return
 * per-tenant thresholds alongside readings so status is derived server-side
 * or using tenant-specific values.
 */
export function deriveRosterItems(snapshots: TenantSnapshot[]): RosterItem[] {
  return snapshots.map((s) => {
    const readings = s.readings ?? [];
    const latest = readings[0];
    const prev = readings[1];

    const mgdl = latest?.mgdl ?? null;
    const prevMgdl = prev?.mgdl ?? null;
    const delta = mgdl != null && prevMgdl != null ? Math.round(mgdl - prevMgdl) : null;

    const now = Date.now();
    const latestTs = latest?.timestamp ? new Date(latest.timestamp).getTime() : null;
    const ageMin = latestTs != null ? Math.floor((now - latestTs) / 60000) : null;

    let status: RosterItem["status"] = "no-data";
    if (mgdl == null) {
      status = "no-data";
    } else if (ageMin != null && ageMin > 25) {
      status = "stale";
    } else if (mgdl < 55) {
      status = "very-low";
    } else if (mgdl < 70) {
      status = "low";
    } else if (mgdl <= 140) {
      status = "tight";
    } else if (mgdl <= 180) {
      status = "in-range";
    } else if (mgdl <= 250) {
      status = "high";
    } else {
      status = "very-high";
    }

    const sparklinePoints = readings
      .slice(0, 36)
      .map((r) => r.mgdl ?? 0)
      .reverse();

    return {
      id: s.tenant.id ?? "",
      slug: s.tenant.slug ?? "",
      displayName: s.tenant.displayName ?? s.tenant.slug ?? "",
      mgdl,
      delta,
      ageMin,
      sparklinePoints,
      status,
      // STUB-BACKEND: TIR — GET /api/v4/platform/roster-snapshots
      tir: { veryLow: 0, low: 0, inRange: 0, high: 0, veryHigh: 0 },
    };
  });
}
