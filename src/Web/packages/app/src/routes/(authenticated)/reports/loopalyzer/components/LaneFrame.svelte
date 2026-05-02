<script lang="ts">
  import type { Snippet } from 'svelte';

  type Props = {
    /** Lane label shown in the gutter (e.g. "BG", "IOB"). */
    title: string;
    /** Pixel height of the lane chart area. BG lane is the dominant ~280px; basal/IOB/COB ~80–120px. */
    height: number;
    /** Whether to render the bottom x-axis (only the last lane in the stack). */
    showXAxis?: boolean;
    /** The chart contents — the lane provides title chrome and sizing; chart composition is the caller's concern. */
    chart: Snippet;
    /** Optional right-side gutter (units label, summary, etc.). */
    rightGutter?: Snippet;
  };

  let { title, height, showXAxis = false, chart, rightGutter }: Props = $props();
</script>

<div class="flex w-full items-stretch border-b border-border last:border-b-0">
  <div class="flex w-12 shrink-0 items-start justify-end pt-1 pr-2 text-xs font-medium text-muted-foreground">
    {title}
  </div>
  <div class="relative flex-1" style="height: {height}px;" class:pb-5={showXAxis}>
    {@render chart()}
  </div>
  {#if rightGutter}
    <div class="flex w-12 shrink-0 items-start pl-2 pt-1 text-xs text-muted-foreground">
      {@render rightGutter()}
    </div>
  {/if}
</div>
