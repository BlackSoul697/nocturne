/**
 * Per-bin aggregation across days. For each 5-min bin, computes the average and
 * the 10th/90th percentile across the contributing days, ignoring nulls.
 */

export interface LaneAggregate {
  avg: (number | null)[];
  p10: (number | null)[];
  p90: (number | null)[];
}

/**
 * Aggregate a 2D array of bins (days × 288) into per-bin avg + P10/P90.
 * Bins with no contributors yield `null` in all three outputs.
 */
export function aggregateLane(
  daysBins: ReadonlyArray<ReadonlyArray<number | null>>,
): LaneAggregate {
  const binCount = daysBins[0]?.length ?? 0;
  const avg: (number | null)[] = new Array(binCount).fill(null);
  const p10: (number | null)[] = new Array(binCount).fill(null);
  const p90: (number | null)[] = new Array(binCount).fill(null);

  for (let i = 0; i < binCount; i++) {
    const samples: number[] = [];
    for (const day of daysBins) {
      const v = day[i];
      if (v != null && Number.isFinite(v)) samples.push(v);
    }
    if (samples.length === 0) continue;
    samples.sort((a, b) => a - b);
    avg[i] = samples.reduce((s, x) => s + x, 0) / samples.length;
    p10[i] = percentile(samples, 0.1);
    p90[i] = percentile(samples, 0.9);
  }

  return { avg, p10, p90 };
}

/** Linear-interpolation percentile on a pre-sorted ascending sample. */
function percentile(sortedAsc: readonly number[], q: number): number {
  if (sortedAsc.length === 1) return sortedAsc[0];
  const pos = q * (sortedAsc.length - 1);
  const base = Math.floor(pos);
  const rest = pos - base;
  const next = sortedAsc[base + 1] ?? sortedAsc[base];
  return sortedAsc[base] + rest * (next - sortedAsc[base]);
}
