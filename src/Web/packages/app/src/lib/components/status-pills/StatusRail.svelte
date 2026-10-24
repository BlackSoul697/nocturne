<script lang="ts">
  import type { Snippet } from "svelte";
  import { browser } from "$app/environment";
  import { RailItemId, type TrackerCategory } from "$lib/api";
  import { getRealtimeStore } from "$lib/stores/realtime-store.svelte";
  import { cn } from "$lib/utils";
  import COBPill from "./COBPill.svelte";
  import BasalPill from "./BasalPill.svelte";
  import IOBPill from "./IOBPill.svelte";
  import LoopPill from "./LoopPill.svelte";
  import ReservoirPill from "./ReservoirPill.svelte";
  import TrackerPillBar from "./TrackerPillBar.svelte";

  interface Props {
    /** The rail items to show, in order. */
    items: readonly RailItemId[];
    /** localStorage key for the number of rows this rail last filled. */
    rowsKey: string;
    onTrackerComplete?: (
      instanceId: string,
      instanceName: string,
      category: TrackerCategory,
      definitionId: string,
      completionEventType?: string
    ) => void;
    /** Shown ahead of the items, inside the same wrapping row. */
    leading?: Snippet;
    class?: string;
    testid?: string;
  }

  let {
    items,
    rowsKey,
    onTrackerComplete,
    leading,
    class: className,
    testid,
  }: Props = $props();

  const realtimeStore = getRealtimeStore();

  // The pills arrive after the page paints and can wrap onto more rows than the one the row
  // starts with, so the rows last rendered under this key are held until they do. Until then
  // the pills are empty placeholders, whose row count says nothing about the loaded ones.
  // StatusPill is 2.75rem tall (text-xs label, text-sm value, py-1) and rows sit gap-y-1 apart.
  const MAX_PILL_ROWS = 4;

  function storedPillRows(): number {
    try {
      const rows = Number(localStorage.getItem(rowsKey));
      return Number.isInteger(rows) && rows > 1 ? Math.min(rows, MAX_PILL_ROWS) : 1;
    } catch {
      return 1;
    }
  }

  let reservedPillRows = $state(browser ? storedPillRows() : 1);
  let pillsEl = $state<HTMLElement>();

  function renderedPillRows(el: HTMLElement): number {
    const children = [...el.children].flatMap((child) =>
      getComputedStyle(child).display === "contents" ? [...child.children] : [child]
    );
    const centres = children
      .map((child) => child.getBoundingClientRect())
      .filter((rect) => rect.height > 0)
      .map((rect) => Math.round(rect.top + rect.height / 2));
    return new Set(centres).size;
  }

  $effect(() => {
    const el = pillsEl;
    const key = rowsKey;
    if (!el || !realtimeStore.isReady) return;
    const observer = new ResizeObserver(() => {
      // A hidden layout fills no rows and says nothing about how many the pills need.
      const rows = renderedPillRows(el);
      if (rows === 0) return;
      reservedPillRows = rows;
      try {
        localStorage.setItem(key, String(rows));
      } catch {
        // Storage is unavailable; the next load reserves one row.
      }
    });
    observer.observe(el);
    return () => observer.disconnect();
  });
</script>

<div
  class={cn(
    "min-h-[calc(var(--pill-rows)*2.75rem+(var(--pill-rows)-1)*0.25rem)] min-w-0",
    className
  )}
  style:--pill-rows={reservedPillRows}
  data-testid={testid}
>
  <div bind:this={pillsEl} class="flex flex-wrap items-center gap-x-2 gap-y-1">
    {@render leading?.()}
    {#each items as item (item)}
      {#if item === RailItemId.Cob}
        <COBPill data={realtimeStore.pillsData.cob} />
      {:else if item === RailItemId.Basal}
        <BasalPill data={realtimeStore.pillsData.basal} />
      {:else if item === RailItemId.Iob}
        <IOBPill data={realtimeStore.pillsData.iob} />
      {:else if item === RailItemId.Loop}
        <LoopPill data={realtimeStore.pillsData.loop} />
      {:else if item === RailItemId.Reservoir}
        <!-- Many pumps and pods report no numeric reservoir (e.g. Omnipod above 50 U). -->
        {#if realtimeStore.currentReservoir !== null}
          <ReservoirPill reservoir={realtimeStore.currentReservoir} />
        {/if}
      {:else if item === RailItemId.Trackers}
        {#if realtimeStore.trackerInstances.length > 0}
          <TrackerPillBar
            instances={realtimeStore.trackerInstances}
            definitions={realtimeStore.trackerDefinitions}
            now={realtimeStore.now}
            onComplete={onTrackerComplete}
            class="contents"
          />
        {/if}
      {/if}
    {/each}
  </div>
</div>
