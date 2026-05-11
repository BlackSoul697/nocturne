<script lang="ts">
  import { invalidate } from "$app/navigation";
  import { page } from "$app/state";
  import { onMount } from "svelte";
  import PlatformSidebar from "$lib/components/platform/PlatformSidebar.svelte";
  import type { LayoutData } from "./$types";

  let { data, children }: { data: LayoutData; children: import("svelte").Snippet } = $props();

  const LIVE_PAGES = ["/roster", "/attention"];
  const POLL_INTERVAL_MS = 60_000;

  const attentionCount = $derived(
    data.snapshots.filter((s) => {
      const latest = s.readings[0];
      if (!latest) return false;
      const mgdl = latest.mgdl ?? 0;
      return mgdl > 0 && (mgdl < 70 || mgdl > 180);
    }).length,
  );

  onMount(() => {
    const interval = setInterval(() => {
      if (LIVE_PAGES.some((p) => $page.url.pathname.startsWith(p))) {
        invalidate("app:roster-snapshots");
      }
    }, POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  });
</script>

<div class="flex h-screen w-full overflow-hidden bg-background">
  <PlatformSidebar
    user={data.user}
    apexHost={data.apexHost}
    {attentionCount}
    tenantCount={data.tenants.length}
  />
  <main class="flex-1 overflow-y-auto">
    {@render children()}
  </main>
</div>
