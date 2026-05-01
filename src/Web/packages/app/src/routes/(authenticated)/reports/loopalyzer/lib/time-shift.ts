/**
 * Per-day time-shift "drop-edges" algorithm. Aligns each day's bin arrays so that
 * the anchor meal lands at the average meal minute across the range. Bins shifted
 * outside `[0, 288)` become null (no neighbour-day pull).
 *
 * Pure TypeScript — no Svelte runtime — to keep it trivially testable.
 */
import type {
  LoopalyzerDay,
  LoopalyzerMeal,
} from "$lib/api/generated/schemas";

export interface ShiftConfig {
  window: { startMin: number; endMin: number };
  minCarbs: number;
  /** Empty list means "all event types". */
  eventTypes: readonly string[];
}

export interface AnchorMeal {
  dayIndex: number;
  minute: number;
  carbs: number;
}

export interface ApplyShiftResult {
  days: LoopalyzerDay[];
  avgMealMinute: number | null;
  /** Per-day shift in 5-minute bins applied to that day. Length matches input. */
  shiftBins: number[];
}

/** Number of bins per day. Mirrors the backend constant. */
const BINS_PER_DAY = 288;
const MIN_PER_BIN = 5;

/**
 * For each day, identify the largest qualifying carb-bearing meal as the anchor.
 * Returns one anchor per qualifying day (others omitted).
 */
export function findAnchorMeals(
  days: readonly LoopalyzerDay[],
  cfg: ShiftConfig,
): AnchorMeal[] {
  const out: AnchorMeal[] = [];
  for (let i = 0; i < days.length; i++) {
    const meals = days[i].meals ?? [];
    const candidate = pickLargestQualifying(meals, cfg);
    if (candidate) {
      out.push({ dayIndex: i, minute: candidate.minute ?? 0, carbs: candidate.carbs ?? 0 });
    }
  }
  return out;
}

/**
 * Apply per-day time shift so each anchor meal aligns to the average anchor minute.
 * Bins shifted outside the [0, 288) range become null.
 */
export function applyShift(
  days: readonly LoopalyzerDay[],
  cfg: ShiftConfig,
): ApplyShiftResult {
  const anchors = findAnchorMeals(days, cfg);
  if (anchors.length === 0) {
    return { days: [...days], avgMealMinute: null, shiftBins: days.map(() => 0) };
  }

  const avgMealMinute = Math.round(
    anchors.reduce((sum, a) => sum + a.minute, 0) / anchors.length,
  );
  const anchorByDay = new Map<number, AnchorMeal>(anchors.map((a) => [a.dayIndex, a]));

  const shiftBins: number[] = [];
  const shiftedDays: LoopalyzerDay[] = days.map((day, i) => {
    const anchor = anchorByDay.get(i);
    if (!anchor) {
      shiftBins.push(0);
      return day;
    }
    const shiftMinutes = avgMealMinute - anchor.minute;
    const offset = Math.round(shiftMinutes / MIN_PER_BIN);
    shiftBins.push(offset);
    if (offset === 0) return day;

    return {
      ...day,
      sgv: rotateNullable(day.sgv ?? null, offset),
      scheduledBasal: rotateNumeric(day.scheduledBasal ?? null, offset),
      tempBasal: rotateNullable(day.tempBasal ?? null, offset),
      iob: rotateNullable(day.iob ?? null, offset),
      cob: rotateNullable(day.cob ?? null, offset),
    };
  });

  return { days: shiftedDays, avgMealMinute, shiftBins };
}

function pickLargestQualifying(
  meals: readonly LoopalyzerMeal[],
  cfg: ShiftConfig,
): LoopalyzerMeal | null {
  let best: LoopalyzerMeal | null = null;
  for (const m of meals) {
    if ((m.carbs ?? 0) < cfg.minCarbs) continue;
    if (cfg.eventTypes.length > 0 && !cfg.eventTypes.includes(m.eventType ?? "")) continue;
    const minute = m.minute ?? 0;
    if (minute < cfg.window.startMin || minute >= cfg.window.endMin) continue;
    if (best == null || (m.carbs ?? 0) > (best.carbs ?? 0)) best = m;
  }
  return best;
}

function rotateNullable(
  bins: ReadonlyArray<number | null> | null,
  offset: number,
): (number | null)[] {
  const out: (number | null)[] = new Array(BINS_PER_DAY).fill(null);
  if (bins == null || offset === 0) {
    for (let i = 0; i < BINS_PER_DAY; i++) out[i] = bins?.[i] ?? null;
    return out;
  }
  for (let i = 0; i < BINS_PER_DAY; i++) {
    const src = i - offset;
    if (src >= 0 && src < BINS_PER_DAY) out[i] = bins[src] ?? null;
  }
  return out;
}

function rotateNumeric(
  bins: ReadonlyArray<number> | null,
  offset: number,
): number[] {
  // Step lines (e.g. scheduled basal) propagate the last-known value into shifted-out
  // edges? Plan says drop-edges; numeric arrays use 0 as neutral fill since `null`
  // isn't representable. Frontend lanes that care can re-detect zeros if needed.
  const out: number[] = new Array(BINS_PER_DAY).fill(0);
  if (bins == null || offset === 0) {
    for (let i = 0; i < BINS_PER_DAY; i++) out[i] = bins?.[i] ?? 0;
    return out;
  }
  for (let i = 0; i < BINS_PER_DAY; i++) {
    const src = i - offset;
    if (src >= 0 && src < BINS_PER_DAY) out[i] = bins[src];
  }
  return out;
}
