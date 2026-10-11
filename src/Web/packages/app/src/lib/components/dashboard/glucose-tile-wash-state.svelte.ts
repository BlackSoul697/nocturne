import { glucoseTileFill, type GlucoseTileVariant } from "@nocturne/ui/glucose";

/** What the tile showed before a wash: a range fill, or the loading skeleton. */
export type PriorFill = GlucoseTileVariant | "skeleton";

// Module state, not instance state: the tile unmounts its wash whenever it shows a flat fill, the
// reading can move between the header tile and the Current Glucose widget, and the wash itself
// is imported only once the page is past its first interactions, so it must outlive any one
// mount. That also means one live wash at a time.
export const washSeeds = $state<{ settled: number | null; faded: number | null }>({
  settled: null,
  faded: null,
});

/** Not reactive: a wash takes it once per reading, not on every change. */
export const unwashed: { fill: PriorFill } = { fill: "skeleton" };

interface TileFill {
  loading: boolean;
  stale: boolean;
  disconnected: boolean;
  variant: GlucoseTileVariant;
}

/**
 * Records the flat fill a host's tile shows while it paints no wash (the skeleton, or what
 * `glucoseTileFill` leaves unpainted), so the next wash blooms over what was on screen. Call
 * during component initialisation.
 */
export function trackUnwashedFill(tile: () => TileFill): void {
  $effect(() => {
    const { loading, variant, ...state } = tile();
    if (loading) {
      unwashed.fill = "skeleton";
      return;
    }
    const { fill, painted } = glucoseTileFill(variant, state);
    if (!painted) unwashed.fill = fill;
  });
}
