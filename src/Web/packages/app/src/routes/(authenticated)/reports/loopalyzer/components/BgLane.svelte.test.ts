import { render } from 'vitest-browser-svelte';
import { page } from 'vitest/browser';
import { describe, it, expect } from 'vitest';
import BgLane from './BgLane.svelte';
import { BIN_COUNT } from '../lib/lane-context.svelte';
import type { LaneAggregate } from '../lib/aggregate';

// Note: assertions stay at the smoke-test level — the lanes render to canvas, so
// pixel-level checks aren't useful. The signal here is "mounts without crashing
// for each prop combination the page can throw at it".

function syntheticAggregate(): LaneAggregate {
  // Simple sine-ish curve so the avg path actually has draw commands.
  const avg: (number | null)[] = [];
  const p10: (number | null)[] = [];
  const p90: (number | null)[] = [];
  for (let i = 0; i < BIN_COUNT; i++) {
    const v = 110 + 40 * Math.sin((i / BIN_COUNT) * Math.PI * 2);
    avg.push(v);
    p10.push(v - 20);
    p90.push(v + 20);
  }
  return { avg, p10, p90 };
}

function syntheticDay(date: string, offset = 0) {
  const sgv: (number | null)[] = [];
  for (let i = 0; i < BIN_COUNT; i++) {
    sgv.push(110 + offset + 40 * Math.sin((i / BIN_COUNT) * Math.PI * 2));
  }
  return { date, sgv };
}

describe('BgLane', () => {
  it('renders the lane label and a canvas in average mode', async () => {
    render(BgLane, {
      aggregate: syntheticAggregate(),
      days: [syntheticDay('2026-05-01')],
      viewMode: 'average',
      bgLow: 70,
      bgHigh: 180,
    });

    await expect.element(page.getByText('BG')).toBeVisible();
  });

  it('renders in spaghetti mode without crashing', async () => {
    render(BgLane, {
      aggregate: syntheticAggregate(),
      days: [
        syntheticDay('2026-04-29', -10),
        syntheticDay('2026-04-30', 0),
        syntheticDay('2026-05-01', 10),
      ],
      viewMode: 'spaghetti',
      todayDate: '2026-05-01',
      bgLow: 70,
      bgHigh: 180,
    });

    await expect.element(page.getByText('BG')).toBeVisible();
  });

  it('renders without target range when bgLow/bgHigh are null', async () => {
    render(BgLane, {
      aggregate: syntheticAggregate(),
      days: [syntheticDay('2026-05-01')],
      viewMode: 'average',
      bgLow: null,
      bgHigh: null,
    });

    await expect.element(page.getByText('BG')).toBeVisible();
  });

  it('renders single-day overlays (predictions, meals, boluses) without throwing', async () => {
    render(BgLane, {
      aggregate: syntheticAggregate(),
      days: [syntheticDay('2026-05-01')],
      viewMode: 'average',
      bgLow: 70,
      bgHigh: 180,
      predictions: [
        {
          minute: 720,
          iob: [120, 125, 130, 135, 140],
          cob: [150, 145, 140, 135, 130],
        },
      ],
      meals: [{ minute: 480, carbs: 60, eventType: 'Meal Bolus' }],
      boluses: [{ minute: 480, units: 6 }],
      siteChanges: [{ minute: 60 }],
      sensorChanges: [{ minute: 90 }],
    });

    await expect.element(page.getByText('BG')).toBeVisible();
  });
});
