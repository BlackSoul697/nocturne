import {
  EventAnchorKind,
  LoopAnalysisSeries,
  type EventCohortResult,
  type EventResponseEvent,
  type EventResponseRange,
  type EventResponseReport,
  type EventResponseSlot,
  type LoopAnalysisAggregate,
} from "$lib/api";

const aggregate = (p50: number, n: number, sparse = false): LoopAnalysisAggregate => ({
  p10: p50 - 2,
  p25: p50 - 1,
  p50,
  p75: p50 + 1,
  p90: p50 + 2,
  mean: p50,
  n,
  sparse,
});

/** A cohort's 72 slots from -60 to +300 minutes, glucose rising 1 mg/dL a slot after the event. */
function cohort(number: number, events: number, offset = 0): EventCohortResult {
  const slots: EventResponseSlot[] = Array.from({ length: 72 }, (_, slot) => {
    const minute = slot * 5 - 60;
    const after = minute >= 0;
    return {
      slot,
      minute,
      glucoseChange: aggregate(after ? slot - 12 + offset : 0, events, events < 5),
      glucose: aggregate(120 + (after ? slot - 12 : 0), events, events < 5),
      netLoopCorrection: aggregate(0.5, events),
      netInsulinSinceAnchor: after ? aggregate(1 + slot * 0.05, events) : undefined,
      netLoopCorrectionSinceAnchor: after ? aggregate(slot * 0.02, events) : undefined,
      iob: aggregate(2.5, events),
      cob: aggregate(20, events),
    };
  });
  return {
    cohort: number,
    anchors: events + 2,
    notIsolated: 1,
    missingAnchorGlucose: 1,
    events,
    slots,
    metrics: {
      startGlucose: aggregate(110, events),
      peakRise: aggregate(65, events),
      minutesToPeak: aggregate(55, events),
      gramsPerUnit: aggregate(9.5, events),
      netInsulin: aggregate(4.2, events),
      netInsulinFirst60: aggregate(1.1, events),
      iobAtAnchor: aggregate(3, events),
    },
  };
}

const event = (date: string, time: string, anchor: EventAnchorKind, cohorts: number[], peakRise: number, grams?: number): EventResponseEvent => ({
  at: `${date}T${time}:00Z`,
  date,
  time,
  anchor,
  grams,
  cohorts,
  metrics: { startGlucose: 110, peakRise, minutesToPeak: 50, netInsulin: 4, netInsulinFirst60: 1, iobAtAnchor: 3 },
});

function range(): EventResponseRange {
  return {
    cohorts: [cohort(1, 12), cohort(2, 3, 5)],
    events: [
      event("2026-10-10", "07:30", EventAnchorKind.Meal, [1], 80, 45),
      event("2026-10-11", "12:05", EventAnchorKind.Meal, [1], 40, 30),
      event("2026-10-12", "19:00", EventAnchorKind.UnannouncedRise, [2], 95),
    ],
  };
}

export function eventResponse(
  options: { comparison?: boolean; withheld?: LoopAnalysisSeries[]; historyClamped?: boolean } = {}
): EventResponseReport {
  const withheld = options.withheld ?? [];
  const report: EventResponseReport = {
    historyClamped: options.historyClamped ?? false,
    ignoredFilters: [],
    unavailableFilters: [],
    unavailableAnchors: [],
    withheldSeries: withheld,
    definition: {
      cohorts: [
        { cohort: 1, anchor: EventAnchorKind.Meal, fromMinute: 300, toMinute: 600 },
        { cohort: 2, anchor: EventAnchorKind.UnannouncedRise },
      ],
      minutesBefore: 60,
      minutesAfter: 300,
      isolatedOnly: false,
      requireBolusCluster: false,
    },
    primary: range(),
    comparison: options.comparison ? range() : undefined,
  };
  if (withheld.includes(LoopAnalysisSeries.EventList) && report.primary) report.primary.events = [];
  for (const r of [report.primary, report.comparison])
    for (const c of r?.cohorts ?? [])
      for (const s of c.slots ?? []) {
        if (withheld.includes(LoopAnalysisSeries.EventGlucose)) s.glucose = s.glucoseChange = undefined;
        if (withheld.includes(LoopAnalysisSeries.EventNetInsulin))
          s.netLoopCorrection = s.netInsulinSinceAnchor = s.netLoopCorrectionSinceAnchor = undefined;
        if (withheld.includes(LoopAnalysisSeries.EventIob)) s.iob = undefined;
        if (withheld.includes(LoopAnalysisSeries.EventCob)) s.cob = undefined;
      }
  return report;
}

export const TREATMENT_SERIES = [
  LoopAnalysisSeries.EventNetInsulin,
  LoopAnalysisSeries.EventIob,
  LoopAnalysisSeries.EventCob,
  LoopAnalysisSeries.EventList,
];
