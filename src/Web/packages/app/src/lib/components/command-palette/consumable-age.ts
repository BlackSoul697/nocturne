import type {
  LegacyConsumableKind,
  LegacyFallbackDto,
  TrackerCategory,
  TrackerInstanceDto,
} from "$lib/api";

/**
 * The age the palette shows for a consumable: the legacy fallback's while no tracker covers it,
 * else the running tracker's of that category. Undefined when neither exists, or when two running
 * trackers share the category and the palette cannot tell which one the reader means.
 */
export function consumableAgeHours(
  kind: LegacyConsumableKind,
  category: TrackerCategory,
  fallbacks: readonly LegacyFallbackDto[],
  instances: readonly TrackerInstanceDto[]
): number | undefined {
  const fallback = fallbacks.find((f) => f.kind === kind);
  if (fallback) return fallback.ageHours;
  const running = instances.filter((i) => i.category === category);
  return running.length === 1 ? running[0].ageHours : undefined;
}
