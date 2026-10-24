import { RailItemId } from "../../api/generated/nocturne-api-client";

/** Every rail item, in the order the default rail shows them. */
export const RAIL_ITEM_IDS: readonly RailItemId[] = Object.values(RailItemId);

/**
 * The rail a viewer who has stored none sees. The tenant's tracker pill switch decides whether
 * trackers are part of it.
 */
export function defaultRail(trackerPillsEnabled: boolean): RailItemId[] {
  return RAIL_ITEM_IDS.filter(
    (id) => id !== RailItemId.Trackers || trackerPillsEnabled
  );
}

/** The viewer's stored rail when there is one, else the default. */
export function effectiveRail(
  stored: readonly RailItemId[] | null | undefined,
  trackerPillsEnabled: boolean
): RailItemId[] {
  return stored ? [...stored] : defaultRail(trackerPillsEnabled);
}

/** Known, distinct rail item ids from a stored value, in their stored order. */
export function knownRailItems(raw: readonly unknown[]): RailItemId[] {
  const items: RailItemId[] = [];
  for (const value of raw) {
    const id = RAIL_ITEM_IDS.find((known) => known === value);
    if (id && !items.includes(id)) items.push(id);
  }
  return items;
}
