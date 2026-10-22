<script lang="ts">
  import { untrack } from "svelte";
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import * as Tabs from "$lib/components/ui/tabs";
  import type { DailyProfileResponse, LoopAnalysisResponse, PredictionAccuracyResponse } from "$lib/api";
  import { getDailyProfile, getPredictionAccuracy } from "$api/generated/loopAnalysis.generated.remote";
  import { requireDateParamsContext } from "$lib/hooks/date-params.svelte";
  import LoopAnalysisFilterBar from "$lib/components/reports/loop-analysis/LoopAnalysisFilterBar.svelte";
  import DailyProfileTab from "$lib/components/reports/loop-analysis/DailyProfileTab.svelte";
  import PredictionsTab from "$lib/components/reports/loop-analysis/PredictionsTab.svelte";
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

  // Only the tab on screen is fetched; its response answers the filter bar as well. Each tab keeps
  // the query it last fetched with, so a tab left keeps its response and is not fetched again on
  // return unless the filter changed meanwhile, and a tab off screen never fetches.
  type TabQuery = typeof query;
  type TabStatus = { stale: boolean; failed: boolean };
  let queries = $state.raw<Partial<Record<LoopAnalysisTab, TabQuery>>>({});
  let status = $state<Partial<Record<LoopAnalysisTab, TabStatus>>>({});
  let dailyProfile = $state<DailyProfileResponse>();
  let predictions = $state<PredictionAccuracyResponse>();

  $effect.pre(() => {
    const tab = filter.tab;
    const next = query;
    const last = untrack(() => queries[tab]);
    if (!last || JSON.stringify(last) !== JSON.stringify(next)) queries = { ...untrack(() => queries), [tab]: next };
  });

  const stale = $derived(status[filter.tab]?.stale ?? false);
  const failed = $derived(status[filter.tab]?.failed ?? false);

  const response = $derived<LoopAnalysisResponse | undefined>(
    filter.tab === "predictions" ? predictions : dailyProfile
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
    { value: "predictions", label: "Predictions" },
  ]);
</script>

<svelte:head>
  <title>Loop Analysis - Nocturne Reports</title>
  <meta
    name="description"
    content="How your automated insulin delivery behaves: what it does at each time of day, and how well its predictions match what followed"
  />
</svelte:head>

{#if queries["predictions"]}
  {@const predictionsQuery = queries["predictions"]}
  <TabResource
    load={() => getPredictionAccuracy(predictionsQuery)}
    onupdate={(state) => {
      predictions = state.current;
      status["predictions"] = { stale: state.stale, failed: state.failed };
    }}
  />
{/if}
{#if queries["daily-profile"]}
  {@const dailyQuery = queries["daily-profile"]}
  <TabResource
    load={() => getDailyProfile(dailyQuery)}
    onupdate={(state) => {
      dailyProfile = state.current;
      status["daily-profile"] = { stale: state.stale, failed: state.failed };
    }}
  />
{/if}

<div class="@container space-y-6">
  <div class="space-y-1 text-muted-foreground print:hidden">
    <p>
      How your loop behaves, observed from your data. Every figure comes with its spread and the
      number of days or predictions behind it.
    </p>
    <ul class="list-disc space-y-0.5 pl-5 text-sm">
      <li>
        <span class="font-medium text-foreground">Daily profile</span>: glucose, basal, the
        correction the loop added and insulin and carbs on board, at each time of day.
      </li>
      <li>
        <span class="font-medium text-foreground">Predictions</span>: how far each curve the loop
        predicted with landed from the readings that followed.
      </li>
    </ul>
  </div>

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
    profileNames={dailyProfile?.primary?.profileNames ?? []}
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
    <Tabs.Content value="predictions" class="mt-4">
      {#if predictions}
        <PredictionsTab response={predictions} />
      {/if}
    </Tabs.Content>
  </Tabs.Root>
</div>
