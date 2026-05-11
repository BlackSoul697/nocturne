<script lang="ts">
  interface Props {
    tir: number;          // 0-100 combined TIR
    attention: number;    // tenants out-of-range right now
    alarms: number;       // STUB-BACKEND: GET /api/v4/platform/roster-snapshots
    stale: number;        // tenants with no reading > 25 min
    tenantCount: number;
  }

  let { tir, attention, alarms, stale, tenantCount }: Props = $props();

  const tirRest = $derived(100 - tir);
</script>

<div class="grid grid-cols-2 gap-3 p-4 sm:grid-cols-4">
  <!-- TIR -->
  <div class="rounded-lg border border-border bg-card p-4">
    <div class="text-xs text-muted-foreground mb-1">Combined TIR · Today</div>
    <div class="flex items-baseline gap-1">
      <span class="text-3xl font-bold tabular-nums">{tir}</span>
      <span class="text-sm text-muted-foreground">%</span>
    </div>
    <div class="mt-2 flex h-1.5 w-full overflow-hidden rounded-full">
      <span style="width:{Math.max(0, tir - 6)}%" class="bg-[var(--glucose-in-range)]"></span>
      <span style="width:6%" class="bg-[var(--glucose-low)]"></span>
      <span style="width:{Math.max(0, tirRest - 6)}%" class="bg-[var(--glucose-high)]"></span>
    </div>
    <div class="mt-1.5 text-xs text-muted-foreground">
      Across {tenantCount} tenants · 24 h rolling
    </div>
  </div>

  <!-- Attention -->
  <div class="rounded-lg border border-border bg-card p-4">
    <div class="text-xs text-muted-foreground mb-1">Needs attention</div>
    <div class="text-3xl font-bold tabular-nums" style="color:{attention > 0 ? 'var(--glucose-low)' : 'inherit'}">
      {attention}
    </div>
    <div class="mt-1 text-xs text-muted-foreground">
      {attention === 0 ? "All readings within range." : "Out-of-range right now"}
    </div>
  </div>

  <!-- Alarms — STUB-BACKEND -->
  <div class="rounded-lg border border-border bg-card p-4">
    <div class="text-xs text-muted-foreground mb-1">Alarms · Today</div>
    <!-- STUB-BACKEND: alarm count per tenant — GET /api/v4/platform/roster-snapshots -->
    <div class="text-3xl font-bold tabular-nums text-muted-foreground">—</div>
    <div class="mt-1 text-xs text-muted-foreground">Not yet available</div>
  </div>

  <!-- Stale -->
  <div class="rounded-lg border border-border bg-card p-4">
    <div class="text-xs text-muted-foreground mb-1">Stale connections</div>
    <div class="text-3xl font-bold tabular-nums" style="color:{stale > 0 ? 'var(--glucose-low)' : 'inherit'}">
      {stale}
    </div>
    <div class="mt-1 text-xs text-muted-foreground">
      {stale === 0 ? "Every CGM reporting." : "No reading > 25 min"}
    </div>
  </div>
</div>
