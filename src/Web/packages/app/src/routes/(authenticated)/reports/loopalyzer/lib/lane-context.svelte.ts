import { getContext, setContext } from 'svelte';

/** Number of 5-minute bins in a 24h day. */
export const BIN_COUNT = 288;

/** Minutes per bin. */
export const BIN_MINUTES = 5;

/** Shared x-axis domain in minutes-of-day, used by every lane. */
export const X_DOMAIN: readonly [number, number] = [0, 1440];

/** Tick positions (minutes) used on the bottom-most lane's x-axis. */
export const X_TICKS = [0, 180, 360, 540, 720, 900, 1080, 1260, 1440];

/** Format a minute-of-day tick as the legacy "0/3/6/.../24" hour label. */
export function formatXTick(minute: number): string {
  return String(Math.round(minute / 60));
}

/** Convert a bin index to its midpoint minute-of-day. */
export function binToMinute(binIndex: number): number {
  return binIndex * BIN_MINUTES + BIN_MINUTES / 2;
}

/**
 * Reactive state shared across the lane stack. Used for cross-lane crosshair
 * hover and for the optional time-shift alignment band. Lane components read
 * via {@link getLaneContext}; the page sets it via {@link createLaneContext}.
 */
export class LaneContextState {
  /** Hovered minute-of-day, or null when no lane is being hovered. */
  hoverMinute = $state<number | null>(null);

  /** Average meal anchor minute when time-shift is active; null otherwise. */
  alignMinute = $state<number | null>(null);

  /** DIA in hours (drives the meal-alignment band width). */
  dia = $state<number | null>(null);
}

const KEY = Symbol('loopalyzer.lane-context');

export function createLaneContext(): LaneContextState {
  const ctx = new LaneContextState();
  setContext(KEY, ctx);
  return ctx;
}

export function getLaneContext(): LaneContextState {
  const ctx = getContext<LaneContextState | undefined>(KEY);
  if (!ctx) throw new Error('LaneContext not set; wrap lanes in <LoopalyzerPage />');
  return ctx;
}
