<script lang="ts">
  import { page } from "$app/state";
  import type { LayoutData } from "../$types";
  import { type RosterItem } from "$lib/components/platform/types";
  import TenantCard from "$lib/components/platform/TenantCard.svelte";

  const data = $derived($page.data as LayoutData);

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

  const critical = $derived(items.filter((i) => i.status === "very-low" || i.status === "very-high"));
  const watching = $derived(items.filter((i) => i.status === "low" || i.status === "high"));
  const stale    = $derived(items.filter((i) => i.status === "stale"));

  function openTenant(item: RosterItem) {
    window.location.href = `https://${item.slug}.${data.apexHost}`;
  }
</script>

<div class="flex flex-col gap-6 p-4">
  <h1 class="text-xl font-semibold">Attention</h1>

  {#each [
    { label: "Critical · respond now", items: critical, color: "var(--glucose-very-low)", pulse: true },
    { label: "Watching",               items: watching, color: "var(--glucose-low)",      pulse: false },
    { label: "Stale connections",      items: stale,    color: "var(--muted-foreground)", pulse: false },
  ] as group}
    {#if group.items.length > 0 || group.label === "Critical · respond now"}
      <section>
        <div class="mb-3 flex items-center gap-2">
          <span
            class="size-2 rounded-full {group.pulse && group.items.length > 0 ? 'animate-pulse' : ''}"
            style="background:{group.color}"
          ></span>
          <h2 class="text-sm font-semibold">{group.label}</h2>
          <span class="rounded-full bg-muted px-1.5 py-0.5 text-xs">{group.items.length}</span>
        </div>
        {#if group.items.length === 0}
          <p class="text-sm text-muted-foreground">All clear.</p>
        {:else}
          <div class="grid gap-3" style="grid-template-columns: repeat(auto-fill, minmax(240px,1fr))">
            {#each group.items as item (item.id)}
              <TenantCard {item} density="standard" onopen={openTenant} />
            {/each}
          </div>
        {/if}
      </section>
    {/if}
  {/each}
</div>
