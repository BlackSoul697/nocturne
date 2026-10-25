import { LegacyConsumableKind } from "$lib/api";

/** The one name each legacy consumable has in the UI. Built per call so wuchale translates it. */
export function legacyKindLabel(kind: LegacyConsumableKind): string {
  switch (kind) {
    case LegacyConsumableKind.Sensor:
      return "Sensor";
    case LegacyConsumableKind.Site:
      return "Infusion site";
    case LegacyConsumableKind.Reservoir:
      return "Reservoir";
    case LegacyConsumableKind.PumpBattery:
      return "Pump battery";
    default: {
      const unhandled: never = kind;
      return unhandled;
    }
  }
}
