/**
 * Shared date/duration presentation helpers for the sleep reports, used by both
 * the trends page and the single-night drill-down. Kept alongside sleep-stages
 * and sleep-night-mapping so the sleep report has one home for its helpers.
 */

/**
 * NSwag-generated fields typed `Date` arrive over the wire as ISO strings
 * (the client's jsonParseReviver is a no-op) — coerce defensively, same
 * pattern used elsewhere in the app (e.g. alerts/[id]/+page.svelte).
 */
export function toDate(value: Date | string | undefined | null): Date | null {
  if (value == null) return null;
  const d = value instanceof Date ? value : new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

/** Format a minute count as "7h 42m" / "45m" / "3h". */
export function formatMinutesDuration(totalMinutes: number): string {
  const total = Math.max(0, Math.round(totalMinutes));
  const h = Math.floor(total / 60);
  const m = total % 60;
  if (h === 0) return `${m}m`;
  if (m === 0) return `${h}h`;
  return `${h}h ${m}m`;
}
