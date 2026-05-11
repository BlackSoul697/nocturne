<script lang="ts">
  import { page } from "$app/state";
  import type { LayoutData } from "../$types";
  import type { RosterItem } from "$lib/components/platform/types";
  import AggregateStrip from "$lib/components/platform/AggregateStrip.svelte";
  import AlertBanner from "$lib/components/platform/AlertBanner.svelte";
  import AttentionRail from "$lib/components/platform/AttentionRail.svelte";
  import RosterToolbar from "$lib/components/platform/RosterToolbar.svelte";
  import TenantCard from "$lib/components/platform/TenantCard.svelte";

  // Data flows from parent layout load
  const data = $derived($page.data as LayoutData);

  let layout = $state<"grid" | "list" | "kanban">("grid");
  let density = $state<"compact" | "standard" | "preview">("standard");
  let sortMode = $state<"name" | "attention" | "tir">("name");

  // Derive RosterItem[] from raw snapshots. Readings are newest-first from the server.
  const items = $derived.by<RosterItem[]>(() =>
    data.snapshots.map((s) => {
      const readings = s.readings ?? [];
      const latest = readings[0];
      const prev = readings[1];

      const mgdl = latest?.mgdl ?? null;
      const prevMgdl = prev?.mgdl ?? null;
      const delta = mgdl != null && prevMgdl != null ? Math.round(mgdl - prevMgdl) : null;

      const now = Date.now();
      const latestTs = latest?.timestamp ? new Date(latest.timestamp).getTime() : null;
      const ageMin = latestTs != null ? Math.floor((now - latestTs) / 60000) : null;

      let status: RosterItem["status"] = "no-data";
      if (mgdl == null) {
        status = "no-data";
      } else if (ageMin != null && ageMin > 25) {
        status = "stale";
      } else if (mgdl < 55) {
        status = "very-low";
      } else if (mgdl < 70) {
        status = "low";
      } else if (mgdl <= 140) {
        status = "tight";
      } else if (mgdl <= 180) {
        status = "in-range";
      } else if (mgdl <= 250) {
        status = "high";
      } else {
        status = "very-high";
      }

      // Reverse so oldest-first for the sparkline
      const sparklinePoints = readings
        .slice(0, 36)
        .map((r) => r.mgdl ?? 0)
        .reverse();

      return {
        id: s.tenant.id ?? "",
        slug: s.tenant.slug ?? "",
        displayName: s.tenant.displayName ?? s.tenant.slug ?? "",
        mgdl,
        delta,
        ageMin,
        sparklinePoints,
        status,
        // STUB-BACKEND: TIR — GET /api/v4/platform/roster-snapshots
        tir: { veryLow: 0, low: 0, inRange: 0, high: 0, veryHigh: 0 },
      };
    })
  );

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

  <!-- List and kanban layouts added in Tasks 11–12 -->
</div>
