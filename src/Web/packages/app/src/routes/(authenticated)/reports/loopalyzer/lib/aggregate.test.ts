import { describe, expect, it } from "vitest";
import { aggregateLane } from "./aggregate";

describe("aggregateLane", () => {
  it("returns nulls when all contributors are null in a bin", () => {
    const result = aggregateLane([[null, null], [null, null]]);
    expect(result.avg).toEqual([null, null]);
    expect(result.p10).toEqual([null, null]);
    expect(result.p90).toEqual([null, null]);
  });

  it("ignores null contributors", () => {
    const result = aggregateLane([[100, null], [200, 50]]);
    expect(result.avg[0]).toBe(150);
    expect(result.avg[1]).toBe(50);
    expect(result.p10[1]).toBe(50);
    expect(result.p90[1]).toBe(50);
  });

  it("single contributor: avg = p10 = p90 = value", () => {
    const result = aggregateLane([[42]]);
    expect(result.avg[0]).toBe(42);
    expect(result.p10[0]).toBe(42);
    expect(result.p90[0]).toBe(42);
  });

  it("five contributors compute percentiles by linear interpolation", () => {
    const result = aggregateLane([[10], [20], [30], [40], [50]]);
    // sorted = [10,20,30,40,50]; n-1 = 4
    // p10: pos = 0.4 -> base=0, rest=0.4 -> 10 + 0.4*(20-10) = 14
    // p90: pos = 3.6 -> base=3, rest=0.6 -> 40 + 0.6*(50-40) = 46
    expect(result.avg[0]).toBe(30);
    expect(result.p10[0]).toBeCloseTo(14, 9);
    expect(result.p90[0]).toBeCloseTo(46, 9);
  });

  it("filters out non-finite values", () => {
    const result = aggregateLane([[Number.NaN, 100], [50, Number.POSITIVE_INFINITY]]);
    expect(result.avg[0]).toBe(50);
    expect(result.avg[1]).toBe(100);
  });

  it("handles empty input", () => {
    const result = aggregateLane([]);
    expect(result.avg).toEqual([]);
    expect(result.p10).toEqual([]);
    expect(result.p90).toEqual([]);
  });
});
