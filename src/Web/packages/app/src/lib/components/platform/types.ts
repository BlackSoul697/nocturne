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
