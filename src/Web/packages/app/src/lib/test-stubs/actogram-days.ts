import type { ActogramDay } from "$lib/components/actogram";

const MS_PER_MINUTE = 60_000;

/** A served day that began `offsetMinutes` east of UTC, `hours` long. */
export function servedDay(date: string, offsetMinutes = 0, hours = 24): ActogramDay {
  const startMills = Date.parse(`${date}T00:00:00Z`) - offsetMinutes * MS_PER_MINUTE;
  return { date, startMills, endMills: startMills + hours * 60 * MS_PER_MINUTE };
}

/** Consecutive served days from `firstDate`, each starting where the last ended. */
export function servedDays(firstDate: string, count: number, offsetMinutes = 0): ActogramDay[] {
  return Array.from({ length: count }, (_, index) => {
    const date = new Date(Date.parse(`${firstDate}T00:00:00Z`) + index * 86_400_000)
      .toISOString()
      .slice(0, 10);
    return servedDay(date, offsetMinutes);
  });
}
