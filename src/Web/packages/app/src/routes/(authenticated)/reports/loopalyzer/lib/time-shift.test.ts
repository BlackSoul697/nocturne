import { describe, expect, it } from "vitest";
import { applyShift, findAnchorMeals } from "./time-shift";
import type {
  LoopalyzerDay,
  LoopalyzerMeal,
} from "$lib/api/generated/schemas";

const BINS_PER_DAY = 288;

function makeDay(opts: {
  meals?: LoopalyzerMeal[];
  sgvFill?: (i: number) => number | null;
}): LoopalyzerDay {
  const sgv = Array.from({ length: BINS_PER_DAY }, (_, i) =>
    opts.sgvFill ? opts.sgvFill(i) : i,
  );
  return {
    date: "2026-05-01",
    sgv,
    scheduledBasal: new Array<number>(BINS_PER_DAY).fill(1),
    tempBasal: new Array<number | null>(BINS_PER_DAY).fill(null),
    iob: new Array<number | null>(BINS_PER_DAY).fill(null),
    cob: new Array<number | null>(BINS_PER_DAY).fill(null),
    meals: opts.meals ?? [],
    boluses: [],
    siteChanges: [],
    sensorChanges: [],
    predictions: [],
    apsBands: [],
    dia: 5,
    hasApsData: false,
  } as LoopalyzerDay;
}

const cfg = {
  window: { startMin: 6 * 60, endMin: 20 * 60 },
  minCarbs: 10,
  eventTypes: [] as string[],
};

describe("findAnchorMeals", () => {
  it("returns empty when no qualifying meals", () => {
    const days = [
      makeDay({ meals: [] }),
      makeDay({ meals: [{ minute: 60, carbs: 5, eventType: "Snack" }] }),
    ];
    expect(findAnchorMeals(days, cfg)).toEqual([]);
  });

  it("picks largest qualifying meal per day", () => {
    const days = [
      makeDay({
        meals: [
          { minute: 7 * 60, carbs: 30, eventType: "Meal" },
          { minute: 12 * 60, carbs: 50, eventType: "Meal" },
          { minute: 22 * 60, carbs: 100, eventType: "Meal" }, // outside window
        ],
      }),
    ];
    const anchors = findAnchorMeals(days, cfg);
    expect(anchors).toEqual([{ dayIndex: 0, minute: 720, carbs: 50 }]);
  });

  it("filters by eventTypes when non-empty", () => {
    const days = [
      makeDay({
        meals: [
          { minute: 7 * 60, carbs: 50, eventType: "Snack" },
          { minute: 8 * 60, carbs: 30, eventType: "Meal Bolus" },
        ],
      }),
    ];
    const anchors = findAnchorMeals(days, { ...cfg, eventTypes: ["Meal Bolus"] });
    expect(anchors).toEqual([{ dayIndex: 0, minute: 480, carbs: 30 }]);
  });
});

describe("applyShift", () => {
  it("returns unchanged days when no qualifying meals", () => {
    const days = [makeDay({}), makeDay({})];
    const result = applyShift(days, cfg);
    expect(result.avgMealMinute).toBeNull();
    expect(result.shiftBins).toEqual([0, 0]);
    expect(result.days[0].sgv?.[0]).toBe(0);
  });

  it("shifts +30 min: first 6 bins null, rest = original 0..281", () => {
    // Day with anchor at 7:00 (420 min) and another with anchor at 7:30 (450 min)
    // avg = 435; day1 shift = 435-420 = 15 min = 3 bins
    const day1 = makeDay({ meals: [{ minute: 7 * 60, carbs: 50, eventType: "Meal" }] });
    const day2 = makeDay({ meals: [{ minute: 7 * 60 + 30, carbs: 50, eventType: "Meal" }] });

    const result = applyShift([day1, day2], cfg);
    expect(result.avgMealMinute).toBe(435);
    expect(result.shiftBins).toEqual([3, -3]);

    // day1 shifted by +3 bins: indices 0..2 null, index 3 = original 0, index 287 = original 284
    expect(result.days[0].sgv?.[0]).toBeNull();
    expect(result.days[0].sgv?.[2]).toBeNull();
    expect(result.days[0].sgv?.[3]).toBe(0);
    expect(result.days[0].sgv?.[287]).toBe(284);
  });

  it("shifts -30 min: indices 0..281 = original 6..287, last 6 null", () => {
    const day1 = makeDay({ meals: [{ minute: 7 * 60 + 30, carbs: 50, eventType: "Meal" }] });
    const day2 = makeDay({ meals: [{ minute: 7 * 60, carbs: 50, eventType: "Meal" }] });

    const result = applyShift([day1, day2], cfg);
    // avg = 435; day1 shift = -15min = -3 bins
    expect(result.shiftBins[0]).toBe(-3);
    expect(result.days[0].sgv?.[0]).toBe(3);
    expect(result.days[0].sgv?.[284]).toBe(287);
    expect(result.days[0].sgv?.[285]).toBeNull();
    expect(result.days[0].sgv?.[287]).toBeNull();
  });

  it("preserves array length 288", () => {
    const day = makeDay({ meals: [{ minute: 7 * 60, carbs: 50, eventType: "Meal" }] });
    const result = applyShift([day, day], cfg);
    for (const d of result.days) {
      expect(d.sgv).toHaveLength(288);
      expect(d.scheduledBasal).toHaveLength(288);
      expect(d.tempBasal).toHaveLength(288);
      expect(d.iob).toHaveLength(288);
      expect(d.cob).toHaveLength(288);
    }
  });

  it("eventTypes filter excludes non-matching anchors", () => {
    const day1 = makeDay({ meals: [{ minute: 7 * 60, carbs: 50, eventType: "Snack" }] });
    const day2 = makeDay({
      meals: [{ minute: 7 * 60 + 30, carbs: 50, eventType: "Meal Bolus" }],
    });

    const result = applyShift([day1, day2], { ...cfg, eventTypes: ["Meal Bolus"] });
    // Only day2 contributes; avg = 450; day1 has no anchor -> shift 0
    expect(result.avgMealMinute).toBe(450);
    expect(result.shiftBins[0]).toBe(0);
  });
});
