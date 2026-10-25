/** A tracker's or a legacy age's elapsed hours, as the pills show them. */
export function formatAge(hours: number | undefined | null): string {
  if (hours === undefined || hours === null) return "n/a";
  if (hours < 1) return `${Math.floor(hours * 60)}m`;
  if (hours < 24) return `${Math.floor(hours)}h`;
  const days = Math.floor(hours / 24);
  const h = Math.floor(hours % 24);
  return h > 0 ? `${days}d ${h}h` : `${days}d`;
}

/** A lifespan in hours, in whole days from one day up. */
export function formatLifespan(hours: number | undefined | null): string {
  if (hours === undefined || hours === null) return "Not set";
  if (hours < 24) return `${hours} hours`;
  const days = Math.floor(hours / 24);
  return days === 1 ? "1 day" : `${days} days`;
}
