import { WARM_IDLE_TIMEOUT_MS } from "@nocturne/watercolour";
import { watercolourDue } from "$lib/stores/illustrations.svelte";
import { lazyComponent, type AnyComponent } from "$lib/utils/lazy-component.svelte";
import { whenIdle } from "$lib/utils/when-idle";

/** Counted from navigation start. */
export const WATERCOLOUR_DEFER_MS = 5000;

export interface DeferredWatercolour {
  /** See {@link watercolourDue}. */
  readonly due: boolean;
}

/**
 * Holds watercolour the page is not for until the first-interaction window is over: five seconds
 * from navigation start, then the next idle period. The engine boot stalls frame presentation for
 * most of a second (measured at 800 ms on a desktop GPU), and the first idle after hydration is
 * when the viewer starts to interact. Artwork that boots the engine as it mounts boots it then,
 * so the engine warm-up and the decorative artwork that would boot it both wait for this.
 *
 * The window is counted from navigation start, so every caller on a page comes due together, and
 * one created after it is due at the next idle period. Under the still presentation nothing boots
 * the engine, so a still is due at once.
 *
 * Call during component initialisation.
 */
export function deferredWatercolour(): DeferredWatercolour {
  let passed = $state(false);

  $effect(() => {
    let cancelIdle: (() => void) | undefined;
    const timer = setTimeout(
      () => {
        cancelIdle = whenIdle(() => (passed = true), { timeout: WARM_IDLE_TIMEOUT_MS });
      },
      Math.max(0, WATERCOLOUR_DEFER_MS - performance.now()),
    );
    return () => {
      clearTimeout(timer);
      cancelIdle?.();
    };
  });

  return {
    get due() {
      return watercolourDue(passed);
    },
  };
}

/**
 * A decorative watercolour component imported once {@link deferredWatercolour} comes due and
 * `wanted` holds, so a page that has not reached it neither parses the library nor boots the
 * engine for it. Render it with `{#if x.component}<x.component … />{/if}`.
 *
 * Call during component initialisation.
 */
export function deferredArtwork<C extends AnyComponent>(
  loader: () => Promise<{ default: C }>,
  wanted: () => boolean = () => true,
): { readonly component: C | null } {
  const lazy = lazyComponent(loader);
  const deferral = deferredWatercolour();
  $effect(() => {
    if (deferral.due && wanted()) lazy.open = true;
  });
  return {
    get component() {
      return lazy.component;
    },
  };
}
