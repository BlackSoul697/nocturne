import {
  DayOfWeek,
  IobCobSource,
  type DailyProfileDay,
  type DailyProfileRange,
  type DailyProfileResponse,
  type DailyProfileSlot,
  type LoopAnalysisAggregate,
} from "$lib/api";

/** A coverage bitmap, laid out as `DailyProfileDayCoverage` documents, with the given slots set. */
export function bitmapOf(slots: number[]): string {
  const bytes = new Uint8Array(36);
  for (const slot of slots) bytes[slot >> 3] |= 1 << (slot & 7);
  return btoa(String.fromCharCode(...bytes));
}

const WEEK = [
  DayOfWeek.Sunday,
  DayOfWeek.Monday,
  DayOfWeek.Tuesday,
  DayOfWeek.Wednesday,
  DayOfWeek.Thursday,
  DayOfWeek.Friday,
  DayOfWeek.Saturday,
];

const ALL_SLOTS =Array.from({ length: 288 }, (_, i) => i);

function aggregate(centre: number, spread: number, n: number, sparse: boolean): LoopAnalysisAggregate {
  return {
    p10: centre - 2 * spread,
    p25: centre - spread,
    p50: centre,
    p75: centre + spread,
    p90: centre + 2 * spread,
    mean: centre,
    n,
    sparse,
  };
}

export interface RangeOptions {
  days?: number;
  /** Scales every value, so a comparison range is told apart from the primary. */
  scale?: number;
  sparse?: boolean;
  withholdTreatments?: boolean;
  withholdSchedule?: boolean;
}

export function profileRange(options: RangeOptions = {}): DailyProfileRange {
  const { days = 14, scale = 1, sparse = false, withholdTreatments = false, withholdSchedule = false } = options;
  const n = sparse ? 2 : days;
  const slots: DailyProfileSlot[] = ALL_SLOTS.map((slot) => ({
    slot,
    minuteOfDay: slot * 5,
    glucose: aggregate(120 * scale, 15, n * 1, sparse),
    scheduledBasal: withholdSchedule ? undefined : aggregate(0.8, 0, n, sparse),
    deliveredBasal: aggregate(0.7 * scale, 0.2, n, sparse),
    netLoopCorrection: {
      mean: 0.4 * scale,
      automaticBolusMean: 0.5 * scale,
      rate: aggregate(-0.1 * scale, 0.1, n, sparse),
    },
    manualTemp: aggregate(0, 0, n, sparse),
    suspend: aggregate(0, 0, n, sparse),
    inferred: aggregate(0, 0, n, sparse),
    ...(withholdTreatments
      ? {}
      : {
          iob: aggregate(1.5 * scale, 0.5, n, sparse),
          iobSource: IobCobSource.Device,
          cob: aggregate(10 * scale, 5, n, sparse),
          cobSource: IobCobSource.Calculated,
          manualBoluses: { meanPerDay: slot === 96 ? 2 * scale : 0, eventsPerDay: slot === 96 ? 0.5 : 0, n, sparse },
          carbs: { meanPerDay: slot === 96 ? 30 * scale : 0, eventsPerDay: slot === 96 ? 0.5 : 0, n, sparse },
        }),
    targetLow: 90,
    targetHigh: 150,
  }));

  const dayList: DailyProfileDay[] = Array.from({ length: n }, (_, i) => {
    const date = `2026-10-${String(i + 1).padStart(2, "0")}`;
    return {
      date,
      dayOfWeek: WEEK[(i + 4) % 7],
      start: `${date}T00:00:00Z`,
      netLoopCorrection: 9.6 * scale,
      totalInsulin: 30,
      coverage: {
        analysed: withholdTreatments ? undefined : bitmapOf(ALL_SLOTS),
        glucose: bitmapOf(ALL_SLOTS),
        scheduledBasal: withholdSchedule ? undefined : bitmapOf(ALL_SLOTS),
        deliveredBasal: bitmapOf(ALL_SLOTS),
        netLoopCorrection: bitmapOf(ALL_SLOTS),
        iobCob: withholdTreatments ? undefined : bitmapOf(ALL_SLOTS),
      },
    };
  });

  return {
    from: "2026-10-01T00:00:00Z",
    to: "2026-10-15T00:00:00Z",
    slotMinutes: 5,
    slots,
    hourlyNetLoopCorrection: Array.from({ length: 24 }, (_, hour) => ({ hour, mean: 0.4 * scale, n, sparse })),
    days: dayList,
    summary: {
      daysWithData: n,
      deliveredToScheduledRatio: 0.88,
      netLoopCorrectionPerDay: 9.6 * scale,
      netLoopCorrectionTddPercent: 32,
      netLoopCorrectionDays: n,
    },
    profileNames: withholdSchedule ? [] : ["Default"],
  };
}

export function profileResponse(
  options: RangeOptions & { comparison?: boolean; historyClamped?: boolean } = {}
): DailyProfileResponse {
  return {
    primary: profileRange(options),
    comparison: options.comparison ? profileRange({ ...options, scale: 0.5 }) : undefined,
    historyClamped: options.historyClamped ?? false,
    ignoredFilters: [],
  };
}
