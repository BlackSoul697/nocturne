<script lang="ts">
  import { page } from "$app/state";
  import type { LayoutData } from "../$types";
  import { deriveRosterItems, dirArrow, type RosterItem } from "$lib/components/platform/types";
  import AggregateStrip from "$lib/components/platform/AggregateStrip.svelte";
  import AlertBanner from "$lib/components/platform/AlertBanner.svelte";
  import AttentionRail from "$lib/components/platform/AttentionRail.svelte";
  import RosterToolbar from "$lib/components/platform/RosterToolbar.svelte";
  import TenantCard from "$lib/components/platform/TenantCard.svelte";
  import TenantListRow from "$lib/components/platform/TenantListRow.svelte";

  // Data flows from parent layout load
  const data = $derived($page.data as LayoutData);

  let layout = $state<"grid" | "list" | "kanban">("grid");
  let density = $state<"compact" | "standard" | "preview">("standard");
  let sortMode = $state<"name" | "attention" | "tir">("name");

  const items = $derived.by<RosterItem[]>(() => deriveRosterItems(data.snapshots));

  const sorted = $derived.by(() => {
    const arr = [...items];
    if (sortMode === "attention") {
      const order: Record<string, number> = {
        "very-low": 0, "very-high": 1, "low": 2, "high": 3,
        "stale": 4, "tight": 5, "in-range": 6, "no-data": 7,
      };
      return arr.sort((a, b) => (order[a.status] ?? 9) - (order[b.status] ?? 9));
    }
    if (sortMode === "tir") return arr.sort((a, b) => a.tir.inRange - b.tir.inRange);
    return arr.sort((a, b) => a.displayName.localeCompare(b.displayName));
  });

  const critical = $derived(sorted.filter((i) => i.status === "very-low" || i.status === "very-high"));
  const attention = $derived(
    sorted.filter((i) => ["very-low", "very-high", "low", "high"].includes(i.status)),
  );

  const agg = $derived({
    tir: items.length ? Math.round(items.reduce((s, i) => s + i.tir.inRange, 0) / items.length) : 0,
    attention: attention.length,
    alarms: 0, // STUB-BACKEND: GET /api/v4/platform/roster-snapshots
    stale: items.filter((i) => i.status === "stale").length,
  });

  function openTenant(item: RosterItem) {
    window.location.href = `https://${item.slug}.${data.apexHost}`;
  }
</script>

<div class="flex flex-col">
  <div class="flex items-center justify-between px-4 py-4 border-b border-border">
    <h1 class="text-xl font-semibold">Roster</h1>
    <span class="text-sm text-muted-foreground font-mono">{data.apexHost}</span>
  </div>

  <AggregateStrip
    tir={agg.tir}
    attention={agg.attention}
    alarms={agg.alarms}
    stale={agg.stale}
    tenantCount={items.length}
  />

  {#if critical.length > 0}
    <AlertBanner {critical} onopen={openTenant} />
  {/if}

  {#if attention.length > 0}
    <AttentionRail items={attention} onopen={openTenant} />
  {/if}

  <RosterToolbar
    {layout}
    {density}
    {sortMode}
    onlayout={(v) => (layout = v)}
    ondensity={(v) => (density = v)}
    onsort={(v) => (sortMode = v)}
  />

  <!-- Grid layout -->
  {#if layout === "grid"}
    <div
      class="p-4 grid gap-3"
      style="grid-template-columns: repeat(auto-fill, minmax({density === 'compact' ? '200px' : density === 'preview' ? '280px' : '240px'}, 1fr))"
    >
      {#each sorted as item (item.id)}
        <TenantCard {item} {density} onopen={openTenant} />
      {/each}
    </div>
  {/if}

  <!-- List layout -->
  {#if layout === "list"}
    <div>
      <!-- Header -->
      <div
        class="grid px-4 py-2 text-xs font-medium text-muted-foreground border-b border-border"
        style="grid-template-columns: 8px 1fr 64px 32px 80px 1fr 60px 48px"
      >
        <span></span><span>Tenant</span><span>BG</span><span>Δ</span>
        <span>3 hr</span><span>Subdomain</span><span>TIR</span><span>Last</span>
      </div>
      {#each sorted as item (item.id)}
        <TenantListRow {item} onopen={openTenant} />
      {/each}
    </div>
  {/if}

  <!-- Kanban layout -->
  {#if layout === "kanban"}
    {@const columns = [
      { key: "very-low",  label: "Very low",  color: "var(--glucose-very-low)" },
      { key: "low",       label: "Low",        color: "var(--glucose-low)" },
      { key: "tight",     label: "Tight",      color: "var(--glucose-tight-range)" },
      { key: "in-range",  label: "In range",   color: "var(--glucose-in-range)" },
      { key: "high",      label: "High",       color: "var(--glucose-high)" },
      { key: "very-high", label: "Very high",  color: "var(--glucose-very-high)" },
      { key: "stale",     label: "Stale",      color: "var(--muted-foreground)" },
      { key: "no-data",   label: "No data",    color: "var(--muted-foreground)" },
    ] as const}
    <div class="flex gap-3 overflow-x-auto p-4 items-start">
      {#each columns as col}
        {@const colItems = sorted.filter((i) => i.status === col.key)}
        {#if colItems.length > 0}
          <div class="flex w-44 shrink-0 flex-col gap-1.5">
            <div class="flex items-center gap-1.5 text-xs font-medium text-muted-foreground">
              <span class="size-2 rounded-full" style="background:{col.color}"></span>
              {col.label} · {colItems.length}
            </div>
            {#each colItems as item (item.id)}
              <button
                class="rounded-lg border bg-card p-2.5 text-left text-xs hover:shadow-sm transition-all"
                style="border-color:{col.color}"
                onclick={() => openTenant(item)}
              >
                <div class="font-medium text-sm">{item.displayName}</div>
                <div class="font-bold tabular-nums mt-1" style="color:{col.color}">
                  {item.mgdl ?? "—"} {dirArrow(item.delta)}
                </div>
                <div class="text-muted-foreground mt-0.5 font-mono truncate">{item.slug}</div>
              </button>
            {/each}
          </div>
        {/if}
      {/each}
    </div>
  {/if}
</div>
