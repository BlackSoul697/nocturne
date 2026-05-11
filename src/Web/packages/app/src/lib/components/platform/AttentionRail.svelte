<script lang="ts">
  import { STATUS_COLOR, dirArrow, type RosterItem } from "./types";

  interface Props {
    items: RosterItem[];
    onopen?: (item: RosterItem) => void;
  }

  let { items, onopen }: Props = $props();
</script>

{#if items.length > 0}
  <div class="border-b border-border px-4 py-3">
    <div class="mb-2 flex items-center gap-2 text-xs font-medium text-muted-foreground">
      <span class="size-1.5 rounded-full bg-[var(--glucose-low)] animate-pulse"></span>
      Needs attention now · {items.length}
    </div>
    <div class="flex gap-2 overflow-x-auto pb-1">
      {#each items as item (item.id)}
        {@const color = STATUS_COLOR[item.status]}
        <button
          class="flex shrink-0 flex-col gap-1 rounded-xl border p-3 text-left transition-all hover:shadow-md"
          style="min-width:200px; border-color:{color}; box-shadow:0 0 0 2px color-mix(in oklch,{color} 18%,transparent)"
          onclick={() => onopen?.(item)}
        >
          <div class="text-sm font-medium">{item.displayName}</div>
          <div class="flex items-baseline gap-1">
            <span class="text-2xl font-bold" style="color:{color}">{item.mgdl ?? "—"}</span>
            <span style="color:{color}">{dirArrow(item.delta)}</span>
          </div>
        </button>
      {/each}
    </div>
  </div>
{/if}
