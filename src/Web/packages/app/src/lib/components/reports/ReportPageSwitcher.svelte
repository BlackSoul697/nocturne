<!--
  Links between the pages of a report made of several (a report category marked `pages`) that the
  viewer can load, for the report header. Each keeps the query string, so the pages share their
  range and filters.
-->
<script lang="ts">
  import { page } from "$app/state";
  import { visibleReportCategories } from "$lib/navigation/report-navigation.svelte";
  import { cn } from "$lib/utils";

  const family = $derived(
    visibleReportCategories({ grantedScopes: page.data.effectivePermissions ?? [], anonymous: !page.data.user }).find(
      (c) => c.pages && c.reports.some((r) => r.href === page.url.pathname)
    )
  );
</script>

{#if family}
  <div class="overflow-x-auto border-t border-border px-3 @md:px-6 print:hidden">
    <nav aria-label="{family.title} pages" class="flex gap-1 py-1.5 whitespace-nowrap" data-testid="report-pages">
      {#each family.reports as report (report.href)}
        {@const current = report.href === page.url.pathname}
        <!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- report.href is a literal in-app path from report-navigation.svelte.ts -->
        <a href="{report.href}{page.url.search}"
          aria-current={current ? "page" : undefined}
          class={cn(
            "rounded-md px-2.5 py-1 text-sm font-medium text-muted-foreground transition-colors hover:bg-muted hover:text-foreground",
            current && "bg-muted text-foreground"
          )}
        >
          {report.sidebarTitle ?? report.title}
        </a>
      {/each}
    </nav>
  </div>
{/if}
