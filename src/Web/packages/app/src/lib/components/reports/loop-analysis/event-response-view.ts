import {
  LoopAnalysisSeries,
  type EventMetrics,
  type EventMetricsSummary,
  type EventResponseEvent,
  type EventResponseSlot,
} from "$lib/api";
import type { BandColors } from "$lib/components/reports/percentile-band-colors";

/** One hue per cohort, in cohort order, so a cohort reads the same in every panel, the table and the list. */
const COHORT_HUES = ["var(--chart-1)", "var(--chart-3)", "var(--chart-2)"];

export function cohortBands(index: number): BandColors {
  const hue = COHORT_HUES[index % COHORT_HUES.length];
  return {
    outer: `oklch(from ${hue} l c h / 0.2)`,
    inner: `oklch(from ${hue} l c h / 0.4)`,
    median: hue,
  };
}

/** Every figure of an event, in the order the table and the list show them. */
export const EVENT_METRICS = [
  "startGlucose",
  "peakRise",
  "minutesToPeak",
  "glucoseAt2Hours",
  "glucoseAt4Hours",
  "minutesBackToStart",
  "totalInsulin",
  "netInsulin",
  "netInsulinFirst30",
  "netInsulinFirst60",
  "netInsulinFirst120",
  "iobAtAnchor",
  "iobAtEnd",
  "gramsPerUnit",
] as const satisfies readonly (keyof EventMetrics & keyof EventMetricsSummary)[];

export type EventMetricKey = (typeof EVENT_METRICS)[number];

/** How a figure is shown: as glucose in the viewer's units, minutes, insulin units, or grams per unit. */
export type MetricKind = "glucose" | "glucoseChange" | "minutes" | "units" | "gramsPerUnit";

export const METRIC_KINDS: Record<EventMetricKey, MetricKind> = {
  startGlucose: "glucose",
  peakRise: "glucoseChange",
  minutesToPeak: "minutes",
  glucoseAt2Hours: "glucose",
  glucoseAt4Hours: "glucose",
  minutesBackToStart: "minutes",
  totalInsulin: "units",
  netInsulin: "units",
  netInsulinFirst30: "units",
  netInsulinFirst60: "units",
  netInsulinFirst120: "units",
  iobAtAnchor: "units",
  iobAtEnd: "units",
  gramsPerUnit: "gramsPerUnit",
};

/** The withheld series each figure leaves with, as the API's guard withholds them. */
export const METRIC_SERIES: Record<EventMetricKey, LoopAnalysisSeries> = {
  startGlucose: LoopAnalysisSeries.EventGlucose,
  peakRise: LoopAnalysisSeries.EventGlucose,
  minutesToPeak: LoopAnalysisSeries.EventGlucose,
  glucoseAt2Hours: LoopAnalysisSeries.EventGlucose,
  glucoseAt4Hours: LoopAnalysisSeries.EventGlucose,
  minutesBackToStart: LoopAnalysisSeries.EventGlucose,
  totalInsulin: LoopAnalysisSeries.EventNetInsulin,
  netInsulin: LoopAnalysisSeries.EventNetInsulin,
  netInsulinFirst30: LoopAnalysisSeries.EventNetInsulin,
  netInsulinFirst60: LoopAnalysisSeries.EventNetInsulin,
  netInsulinFirst120: LoopAnalysisSeries.EventNetInsulin,
  iobAtAnchor: LoopAnalysisSeries.EventIob,
  iobAtEnd: LoopAnalysisSeries.EventIob,
  gramsPerUnit: LoopAnalysisSeries.EventNetInsulin,
};

/** The slots where the relative axis is ticked: each whole hour from the anchor. */
export function hourTicks(slots: readonly EventResponseSlot[]): number[] {
  const ticks = slots.filter((s) => (s.minute ?? 0) % 60 === 0).map((s) => s.slot ?? 0);
  const last = slots.at(-1);
  if (last && ((last.minute ?? 0) + 5) % 60 === 0) ticks.push((last.slot ?? 0) + 1);
  return ticks;
}

/** The minute from the anchor slot `slot` starts at; the window's end for the slot past the last. */
export function slotMinute(slots: readonly EventResponseSlot[], slot: number): number {
  return slots[slot]?.minute ?? (slots.at(-1)?.minute ?? 0) + 5;
}

export type EventSortKey = "at" | EventMetricKey;

/** Events ordered by `key`; events without the figure last, whichever the direction. */
export function sortEvents(
  events: readonly EventResponseEvent[],
  key: EventSortKey,
  descending: boolean
): EventResponseEvent[] {
  const value = (e: EventResponseEvent): number | null =>
    key === "at" ? new Date(e.at ?? 0).getTime() : (e.metrics?.[key] ?? null);
  return [...events].sort((a, b) => {
    const x = value(a);
    const y = value(b);
    if (x == null || y == null) return x == null ? (y == null ? 0 : 1) : -1;
    return descending ? y - x : x - y;
  });
}
