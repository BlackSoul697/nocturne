<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import * as Tabs from "$lib/components/ui/tabs";
  import { getDailyProfile } from "$api/generated/loopAnalysis.generated.remote";
  import { requireDateParamsContext } from "$lib/hooks/date-params.svelte";
  import { contextResource } from "$lib/hooks/resource-context.svelte";
  import LoopAnalysisFilterBar from "$lib/components/reports/loop-analysis/LoopAnalysisFilterBar.svelte";
  import DailyProfileTab from "$lib/components/reports/loop-analysis/DailyProfileTab.svelte";
  import {
    dailyProfileQuery,
    isLoopAnalysisTab,
    readLoopAnalysisFilter,
    writeLoopAnalysisFilter,
    type LoopAnalysisFilterState,
    type LoopAnalysisTab,
  } from "$lib/components/reports/loop-analysis/loop-analysis-filter";

  const reportsParams = requireDateParamsContext(14);

  const filter = $derived(readLoopAnalysisFilter(page.url.searchParams));

  function setFilter(next: LoopAnalysisFilterState) {
    const search = writeLoopAnalysisFilter(next, page.url.searchParams).toString();
    void goto(resolve(`/reports/loop-analysis?${search}`), {
      replaceState: true,
      noScroll: true,
      keepFocus: true,
    });
  }

  const requested = $derived({ startDate: reportsParams.fromDay, endDate: reportsParams.toDay });
  const query = $derived(dailyProfileQuery(filter, requested));

  const profileResource = contextResource(() => getDailyProfile(query), {
    errorTitle: "Error Loading Loop Analysis",
  });
  const response = $derived(profileResource.current);
  const clamped = $derived(response?.historyClamped ?? page.data.limitTo24Hours ?? false);

  const insulinDeliveryHref = $derived.by(() => {
    const range = ["days", "from", "to", "isDefault"]
      .filter((name) => page.url.searchParams.has(name))
      .map((name) => `${name}=${encodeURIComponent(page.url.searchParams.get(name) ?? "")}`)
      .join("&");
    return resolve(`/reports/insulin-delivery?${range}`);
  });

  const tabs = $derived<{ value: LoopAnalysisTab; label: string }[]>([
    { value: "daily-profile", label: "Daily profile" },
  ]);
</script>

<svelte:head>
  <title>Loop Analysis - Nocturne Reports</title>
  <meta
    name="description"
    content="How your automated insulin delivery behaves across the day: glucose, basal, loop corrections, IOB and COB by time of day"
  />
</svelte:head>

<div class="@container space-y-6">
  <p class="text-muted-foreground print:hidden">
    What your loop did at each time of day: glucose, scheduled against delivered basal, the
    correction the algorithm added on top, and insulin and carbs on board. Every figure is
    observed from your data, with its spread and the number of days behind it.
  </p>

  <LoopAnalysisFilterBar
    {filter}
    onchange={setFilter}
    {requested}
    primaryRange={response?.primaryRange}
    comparisonRange={response?.comparisonRange}
    applied={response?.appliedFilters}
    {clamped}
    grantedScopes={page.data.effectivePermissions ?? []}
    profileNames={response?.primary?.profileNames ?? []}
  />

  <Tabs.Root
    value={filter.tab}
    onValueChange={(tab) => {
      if (isLoopAnalysisTab(tab)) setFilter({ ...filter, tab });
    }}
  >
    <Tabs.List class="print:hidden">
      {#each tabs as tab (tab.value)}
        <Tabs.Trigger value={tab.value}>{tab.label}</Tabs.Trigger>
      {/each}
    </Tabs.List>
    <Tabs.Content value="daily-profile" class="mt-4">
      {#if response}
        <DailyProfileTab {response} {insulinDeliveryHref} />
      {/if}
    </Tabs.Content>
  </Tabs.Root>
</div>
