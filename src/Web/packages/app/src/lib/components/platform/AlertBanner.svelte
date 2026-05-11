<script lang="ts">
  import { AlertTriangle } from "lucide-svelte";
  import type { RosterItem } from "./types";

  interface Props {
    critical: RosterItem[];
    onopen?: (item: RosterItem) => void;
  }

  let { critical, onopen }: Props = $props();
  const first = $derived(critical[0]);
  const others = $derived(critical.length - 1);
</script>

{#if critical.length > 0 && first}
  <div class="flex items-center gap-3 px-4 py-2.5 text-sm"
       style="background:color-mix(in oklch,var(--glucose-very-low) 12%,var(--background))">
    <AlertTriangle class="size-4 shrink-0 text-[var(--glucose-very-low)]" />
    <div class="flex-1">
      <span class="font-semibold">{first.displayName}</span>
      {' '}at{' '}
      <span class="font-semibold text-[var(--glucose-very-low)]">{first.mgdl} mg/dL</span>
      {#if others > 0}
        {' '}· plus <strong>{others}</strong> other{others > 1 ? "s" : ""} flagged
      {/if}
    </div>
    <button class="rounded border border-border px-2 py-1 text-xs hover:bg-accent"
            onclick={() => onopen?.(first)}>
      Open {first.displayName.split(" ")[0]}
    </button>
  </div>
{/if}
