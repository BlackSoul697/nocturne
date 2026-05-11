<script lang="ts">
  import { invalidate } from "$app/navigation";
  import { page } from "$app/state";
  import { onMount } from "svelte";
  import PlatformSidebar from "$lib/components/platform/PlatformSidebar.svelte";
  import { deriveRosterItems } from "$lib/components/platform/types";
  import type { LayoutData } from "./$types";

  let { data, children }: { data: LayoutData; children: import("svelte").Snippet } = $props();

  const LIVE_PAGES = ["/roster", "/attention"];
  const POLL_INTERVAL_MS = 60_000;

  const rosterItems = $derived(deriveRosterItems(data.snapshots));
  const attentionCount = $derived(
    rosterItems.filter((i) => ["very-low", "very-high", "low", "high"].includes(i.status)).length,
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
