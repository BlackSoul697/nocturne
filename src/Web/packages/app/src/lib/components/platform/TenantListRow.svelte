<script lang="ts">
  import { RosterSparkline } from "@nocturne/ui";
  import { STATUS_COLOR, dirArrow, ageStr, type RosterItem } from "./types";

  interface Props {
    item: RosterItem;
    onopen?: (item: RosterItem) => void;
  }

  let { item, onopen }: Props = $props();
  const color = $derived(STATUS_COLOR[item.status]);
</script>

<div
  class="grid items-center gap-3 px-4 py-2.5 text-sm border-b border-border hover:bg-accent/30 cursor-pointer transition-colors"
  style="grid-template-columns: 8px 1fr 64px 32px 80px 1fr 60px 48px"
  role="button"
  tabindex="0"
  onclick={() => onopen?.(item)}
  onkeydown={(e) => e.key === "Enter" && onopen?.(item)}
>
  <span class="size-2 rounded-full" style="background:{color}"></span>
  <div>
    <div class="font-medium">{item.displayName}</div>
  </div>
  <div class="font-bold tabular-nums" style="color:{color}">{item.mgdl ?? "—"}</div>
  <div class="text-muted-foreground">{dirArrow(item.delta)}</div>
  <div class="h-7 w-20">
    {#if item.sparklinePoints.length > 1}
      <RosterSparkline points={item.sparklinePoints} {color} band={false} />
    {/if}
  </div>
  <div class="truncate text-xs text-muted-foreground font-mono">{item.slug}</div>
  <div class="tabular-nums text-xs">TIR <strong>{item.tir.inRange}%</strong></div>
  <div class="text-xs text-muted-foreground">{ageStr(item.ageMin)}</div>
</div>
