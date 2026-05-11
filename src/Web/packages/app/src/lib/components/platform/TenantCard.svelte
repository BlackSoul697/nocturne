<script lang="ts">
  import { RosterSparkline, TIRBar } from "@nocturne/ui";
  import { STATUS_COLOR, STATUS_LABEL, dirArrow, ageStr, type RosterItem } from "./types";

  interface Props {
    item: RosterItem;
    density?: "compact" | "standard" | "preview";
    onopen?: (item: RosterItem) => void;
  }

  let { item, density = "standard", onopen }: Props = $props();

  const color = $derived(STATUS_COLOR[item.status]);
  const isAlerting = $derived(item.status === "very-low" || item.status === "very-high");
</script>

<button
  class="group relative flex flex-col gap-2 rounded-xl border bg-card p-3 text-left transition-all hover:shadow-md w-full"
  style="--status-color:{color}; border-color:{isAlerting ? color : 'var(--border)'};
         {isAlerting ? `box-shadow: 0 0 0 3px color-mix(in oklch, ${color} 18%, transparent)` : ''}"
  onclick={() => onopen?.(item)}
>
  <!-- Header row -->
  <div class="flex items-start gap-2">
    <div class="relative flex size-8 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-semibold">
      {item.displayName.split(" ").map((p: string) => p[0]).slice(0, 2).join("").toUpperCase()}
      <span class="absolute -bottom-0.5 -right-0.5 size-2.5 rounded-full border-2 border-card"
            style="background:{color}"></span>
    </div>
    <div class="flex-1 min-w-0">
      <div class="truncate text-sm font-medium">{item.displayName}</div>
      {#if density !== "compact"}
        <div class="truncate text-xs text-muted-foreground font-mono">{item.slug}</div>
      {/if}
    </div>
    {#if density === "standard" || density === "preview"}
      <span class="flex items-center gap-1 rounded-full px-1.5 py-0.5 text-xs"
            style="background:color-mix(in oklch,{color} 18%,transparent)">
        <span class="size-1.5 rounded-full" style="background:{color}"></span>
        {STATUS_LABEL[item.status]}
      </span>
    {/if}
  </div>

  <!-- BG row -->
  <div class="flex items-baseline gap-1.5">
    <span class="text-2xl font-bold tabular-nums leading-none" style="color:{color}">
      {item.mgdl ?? "—"}
    </span>
    <span class="text-lg" style="color:{color}">{dirArrow(item.delta)}</span>
    {#if density !== "compact"}
      <div class="flex flex-col text-xs text-muted-foreground">
        <span>{item.delta != null ? `${item.delta >= 0 ? "+" : ""}${item.delta} mg/dL` : ""}</span>
        <span class="{item.status === 'stale' ? 'text-[var(--glucose-low)]' : ''}">{ageStr(item.ageMin)}</span>
      </div>
    {/if}
  </div>

  <!-- Sparkline (standard + preview) -->
  {#if density !== "compact" && item.sparklinePoints.length > 1}
    <div class="h-9 w-full">
      <RosterSparkline points={item.sparklinePoints} {color} />
    </div>
  {/if}

  <!-- TIR bar (standard + preview) -->
  {#if density !== "compact"}
    <TIRBar tir={item.tir} />
  {/if}

  <!-- Footer (preview only) -->
  {#if density === "preview"}
    <div class="flex items-center justify-between text-xs text-muted-foreground">
      <span class="font-mono">{item.slug}</span>
      <span>Open ↗</span>
    </div>
  {/if}
</button>
