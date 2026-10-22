import type {
  LoopHealthDistribution,
  LoopHealthGap,
  LoopHealthGaps,
  LoopHealthHourValue,
  LoopHealthShare,
  LoopHealthShareHour,
} from "$lib/api";
import type { SlotBar } from "./SlotBars.svelte";
import { clockTime } from "./daily-profile-view";

/** Five-minute slots in an hour: the hour strips share the slot axis of the Daily profile. */
export const SLOTS_PER_HOUR = 12;

export const hourOfSlot = (slot: number) => Math.floor(slot / SLOTS_PER_HOUR);

export function shareHour(share: LoopHealthShare | null | undefined, hour: number): LoopHealthShareHour | undefined {
  return share?.hours?.find((h) => h.hour === hour);
}

export function distributionHour(
  distribution: LoopHealthDistribution | null | undefined,
  hour: number
): LoopHealthHourValue | undefined {
  return distribution?.hours?.find((h) => h.hour === hour);
}

/** A share's hours as bars over each hour's twelve slots; an hour with nothing measured has none. */
export function shareBars(share: LoopHealthShare | null | undefined): SlotBar[] {
  return (share?.hours ?? [])
    .filter((h) => h.percent != null)
    .map((h) => ({
      start: (h.hour ?? 0) * SLOTS_PER_HOUR,
      end: ((h.hour ?? 0) + 1) * SLOTS_PER_HOUR,
      value: h.percent!,
      sparse: h.daily?.sparse ?? true,
    }));
}

export type GapKind = "loop" | "unrecorded";

export interface GapRow {
  kind: GapKind;
  gap: LoopHealthGap;
}

/** Both kinds of gap in one list, longest first, as the API reported each. */
export function gapRows(loopGaps: LoopHealthGaps | null | undefined, unrecorded: LoopHealthGaps | null | undefined): GapRow[] {
  return [
    ...(loopGaps?.gaps ?? []).map((gap) => ({ kind: "loop" as const, gap })),
    ...(unrecorded?.gaps ?? []).map((gap) => ({ kind: "unrecorded" as const, gap })),
  ].sort((a, b) => (b.gap.minutes ?? 0) - (a.gap.minutes ?? 0));
}

/** The Day in Review search for the local day and time a gap began at. */
export function dayInReviewSearch(gap: LoopHealthGap): string {
  return `date=${gap.date ?? ""}&time=${clockTime(gap.startMinuteOfDay ?? 0)}`;
}
