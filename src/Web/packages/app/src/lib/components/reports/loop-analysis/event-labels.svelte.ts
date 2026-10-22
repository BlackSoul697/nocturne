import { EventAnchorKind } from "$lib/api";
import { bg, bgDelta, bgLabel } from "$lib/utils/formatting";
import type { EventMetricKey, MetricKind } from "./event-response-view";
import { ignoredFilterLabel } from "./filter-labels.svelte";

/** Built per call so wuchale reads them in the current locale. */
export function anchorLabels(): Record<EventAnchorKind, string> {
  return {
    [EventAnchorKind.Meal]: "Meals",
    [EventAnchorKind.CorrectionBolus]: "Correction boluses",
    [EventAnchorKind.SensorStart]: "Sensor starts",
    [EventAnchorKind.ExerciseStart]: "Exercise starts",
    [EventAnchorKind.OverrideStart]: "Override starts",
    [EventAnchorKind.TemporaryTargetStart]: "Temporary target starts",
    [EventAnchorKind.UnannouncedRise]: "Unannounced rises",
  };
}

export function anchorLabel(kind: EventAnchorKind | undefined): string {
  return kind ? anchorLabels()[kind] : "";
}

/** One event's anchor, in the event list. */
export function anchorEventLabel(kind: EventAnchorKind | undefined): string {
  switch (kind) {
    case EventAnchorKind.Meal:
      return "Meal";
    case EventAnchorKind.CorrectionBolus:
      return "Correction bolus";
    case EventAnchorKind.SensorStart:
      return "Sensor start";
    case EventAnchorKind.ExerciseStart:
      return "Exercise";
    case EventAnchorKind.OverrideStart:
      return "Override";
    case EventAnchorKind.TemporaryTargetStart:
      return "Temporary target";
    case EventAnchorKind.UnannouncedRise:
      return "Unannounced rise";
    default:
      return "";
  }
}

export function metricLabels(): Record<EventMetricKey, string> {
  return {
    startGlucose: "Start glucose",
    peakRise: "Peak rise",
    minutesToPeak: "Time to peak",
    glucoseAt2Hours: "Glucose at 2 h",
    glucoseAt4Hours: "Glucose at 4 h",
    minutesBackToStart: "Back within 20 mg/dL of start",
    totalInsulin: "Total insulin in window",
    netInsulin: "Net insulin above schedule in window",
    netInsulinFirst30: "Above schedule, first 30 min",
    netInsulinFirst60: "Above schedule, first 60 min",
    netInsulinFirst120: "Above schedule, first 2 h",
    iobAtAnchor: "IOB at the event",
    iobAtEnd: "IOB at window end",
    gramsPerUnit: "Observed grams per unit",
  };
}

/** A name from the response's `ignoredFilters`, including the builder's parameters. */
export function eventIgnoredFilterLabel(name: string): string {
  const cohort = /^anchor(\d)$/.exec(name);
  if (cohort) return `Cohort ${cohort[1]}`;
  if (name === "isolatedOnly") return "Isolated events only";
  if (name === "requireBolusCluster") return "Require automatic boluses";
  return ignoredFilterLabel(name);
}

/** Minutes from the event, signed, as the relative axis and its tooltip read them. */
export function relativeMinutes(minute: number): string {
  if (minute === 0) return "Event";
  return minute > 0 ? `+${minute} min` : `−${-minute} min`;
}

/** A figure in its kind's display units, unit included. */
export function formatMetric(kind: MetricKind, value: number | null | undefined): string {
  if (value == null) return "–";
  switch (kind) {
    case "glucose":
      return `${bg(value)} ${bgLabel()}`;
    case "glucoseChange":
      return `${bgDelta(value)} ${bgLabel()}`;
    case "minutes":
      return `${Math.round(value)} min`;
    case "units":
      return `${value.toFixed(2)} U`;
    case "gramsPerUnit":
      return `${value.toFixed(1)} g/U`;
  }
}

/** A whole-hour tick of the relative axis. */
export function relativeHours(minute: number): string {
  if (minute === 0) return "0";
  const hours = minute / 60;
  return hours > 0 ? `+${hours} h` : `−${-hours} h`;
}
