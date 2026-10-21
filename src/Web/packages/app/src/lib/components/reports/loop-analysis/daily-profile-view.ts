import type {
  DailyProfileDay,
  DailyProfileDayCoverage,
  DailyProfileRange,
  DailyProfileSlot,
  LoopAnalysisAggregate,
} from "$lib/api";

export const SLOTS_PER_DAY = 288;
export const SLOT_MINUTES = 5;

const decoded = new Map<string, Uint8Array>();

function bitmap(base64: string): Uint8Array {
  let bytes = decoded.get(base64);
  if (!bytes) {
    bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
    decoded.set(base64, bytes);
  }
  return bytes;
}

/** Reads one slot of a coverage bitmap, laid out as `DailyProfileDayCoverage` documents. */
export function covers(base64: string | undefined | null, slot: number): boolean {
  if (!base64) return false;
  const bytes = bitmap(base64);
  return ((bytes[slot >> 3] ?? 0) & (1 << (slot & 7))) !== 0;
}

export type CoverageSeries = Exclude<keyof DailyProfileDayCoverage, "analysed">;

export const COVERAGE_SERIES: CoverageSeries[] = [
  "glucose",
  "scheduledBasal",
  "deliveredBasal",
  "netLoopCorrection",
  "iobCob",
];

export interface ContributingDay {
  day: DailyProfileDay;
  series: CoverageSeries[];
}

/**
 * The days that went into a slot. `analysed` decides where the response carries it; where it is
 * withheld, a day counts when any series it does carry covered the slot.
 */
export function contributingDays(range: DailyProfileRange | undefined, slot: number): ContributingDay[] {
  const result: ContributingDay[] = [];
  for (const day of range?.days ?? []) {
    const coverage = day.coverage ?? {};
    const series = COVERAGE_SERIES.filter((s) => covers(coverage[s], slot));
    const analysed = coverage.analysed != null ? covers(coverage.analysed, slot) : series.length > 0;
    if (analysed) result.push({ day, series });
  }
  return result;
}

/** Whether every day of the range has this series' bitmap withheld from the caller. */
export function withheld(range: DailyProfileRange | undefined, series: CoverageSeries): boolean {
  const days = range?.days ?? [];
  return days.length > 0 && days.every((d) => d.coverage?.[series] == null);
}

export interface SlotPoint<T> {
  slot: number;
  value: T;
}

export interface SlotRun<T> {
  sparse: boolean;
  points: SlotPoint<T>[];
}

export type AggregatePoint = SlotPoint<LoopAnalysisAggregate>;

/**
 * Consecutive slots holding the series, split where it is absent (a gap, never drawn as zero) and
 * where its sparse flag changes. A run ending at a sparseness change repeats the next run's first
 * point so the two meet instead of leaving a sliver.
 */
export function slotRuns<T>(
  slots: DailyProfileSlot[],
  pick: (slot: DailyProfileSlot) => T | null | undefined,
  isSparse: (value: T, slot: DailyProfileSlot) => boolean
): SlotRun<T>[] {
  const runs: SlotRun<T>[] = [];
  let current: SlotRun<T> | null = null;
  for (const slot of slots) {
    const value = pick(slot);
    const index = slot.slot ?? 0;
    if (value == null) {
      current = null;
      continue;
    }
    const sparse = isSparse(value, slot);
    const point = { slot: index, value };
    if (current && current.sparse === sparse) {
      current.points.push(point);
      continue;
    }
    current?.points.push(point);
    current = { sparse, points: [point] };
    runs.push(current);
  }
  return runs;
}

export function aggregateRuns(
  slots: DailyProfileSlot[],
  pick: (slot: DailyProfileSlot) => LoopAnalysisAggregate | undefined
): SlotRun<LoopAnalysisAggregate>[] {
  return slotRuns(slots, pick, (value) => value.sparse ?? false);
}

/**
 * What a key does on a time-of-day panel: move the cursor by a number of slots (an hour with
 * Shift), or pick the slot. Kept out of the component so wuchale does not translate the key names.
 */
export function slotKeyAction(event: KeyboardEvent): number | "select" | null {
  const step = event.shiftKey ? 12 : 1;
  switch (event.key) {
    case "ArrowRight":
      return step;
    case "ArrowLeft":
      return -step;
    case "Enter":
    case " ":
      return "select";
    default:
      return null;
  }
}

/** `HH:mm` for a minute of the local day, as Day in Review's `time` parameter reads it. */
export function clockTime(minuteOfDay: number): string {
  const h = Math.floor(minuteOfDay / 60) % 24;
  const m = minuteOfDay % 60;
  return `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}`;
}

/** A wall-clock instant on an arbitrary date, for formatting a slot's time with the viewer's clock format. */
export function atMinute(minuteOfDay: number): Date {
  return new Date(2000, 0, 1, 0, minuteOfDay);
}
