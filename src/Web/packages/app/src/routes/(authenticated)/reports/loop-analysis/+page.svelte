<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import * as Tabs from "$lib/components/ui/tabs";
  import type { DailyProfileResponse, LoopAnalysisResponse, LoopHealthResponse } from "$lib/api";
  import { getDailyProfile, getLoopHealth } from "$api/generated/loopAnalysis.generated.remote";
  import { requireDateParamsContext } from "$lib/hooks/date-params.svelte";
  import LoopAnalysisFilterBar from "$lib/components/reports/loop-analysis/LoopAnalysisFilterBar.svelte";
  import DailyProfileTab from "$lib/components/reports/loop-analysis/DailyProfileTab.svelte";
  import LoopHealthTab from "$lib/components/reports/loop-analysis/LoopHealthTab.svelte";
  import TabResource from "$lib/components/reports/loop-analysis/TabResource.svelte";
  import {
    dailyProfileQuery,
    isLoopAnalysisTab,
    readLoopAnalysisFilter,
    withoutUnavailable,
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

  // Only the tab on screen is fetched; its response answers the filter bar as well.
  let dailyProfile = $state<DailyProfileResponse>();
  let loopHealth = $state<LoopHealthResponse>();
  let stale = $state(false);
  let failed = $state(false);

  const response = $derived<LoopAnalysisResponse | undefined>(
    filter.tab === "loop-health" ? loopHealth : dailyProfile
  );
  // A failed request is settled too, so the bar shows what was picked rather than loading forever;
  // its echoes are then the last response's, which no longer answer the request on screen.
  const settled = $derived(failed || (response != null && !stale));
  const echo = $derived(settled && !failed ? response : null);
  const clamped = $derived(response?.historyClamped ?? page.data.limitTo24Hours ?? false);

  // A shared link may carry a filter this viewer cannot use. Once the API has said which those
  // are, they leave the URL, and so the request, rather than standing as an ignored filter.
  $effect(() => {
    const unavailable = response?.unavailableFilters?.map((f) => f.filter ?? "") ?? [];
    const next = withoutUnavailable(filter, unavailable);
    if (next) setFilter(next);
  });

  const insulinDeliveryHref = $derived.by(() => {
    const range = ["days", "from", "to", "isDefault"]
      .filter((name) => page.url.searchParams.has(name))
      .map((name) => `${name}=${encodeURIComponent(page.url.searchParams.get(name) ?? "")}`)
      .join("&");
    return resolve(`/reports/insulin-delivery?${range}`);
  });

  const tabs = $derived<{ value: LoopAnalysisTab; label: string }[]>([
    { value: "daily-profile", label: "Daily profile" },
    { value: "loop-health", label: "Loop health" },
  ]);
</script>

<svelte:head>
  <title>Loop Analysis - Nocturne Reports</title>
  <meta
    name="description"
    content="How your automated insulin delivery behaves across the day: glucose, basal, loop corrections, IOB and COB by time of day"
  />
</svelte:head>

{#if filter.tab === "loop-health"}
  <TabResource
    load={() => getLoopHealth(query)}
    onupdate={(state) => {
      loopHealth = state.current;
      ({ stale, failed } = state);
    }}
  />
{:else}
  <TabResource
    load={() => getDailyProfile(query)}
    onupdate={(state) => {
      dailyProfile = state.current;
      ({ stale, failed } = state);
    }}
  />
{/if}

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
    primaryRange={echo?.primaryRange}
    comparisonRange={echo?.comparisonRange}
    applied={echo?.appliedFilters}
    {settled}
    {clamped}
    unavailable={response?.unavailableFilters}
    profileNames={response?.profileNames ?? []}
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
      {#if dailyProfile}
        <DailyProfileTab response={dailyProfile} {insulinDeliveryHref} />
      {/if}
    </Tabs.Content>
    <Tabs.Content value="loop-health" class="mt-4">
      {#if loopHealth}
        <LoopHealthTab response={loopHealth} />
      {/if}
    </Tabs.Content>
  </Tabs.Root>
</div>
