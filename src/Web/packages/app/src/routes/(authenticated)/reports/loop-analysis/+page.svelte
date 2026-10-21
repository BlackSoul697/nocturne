<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import * as Tabs from "$lib/components/ui/tabs";
  import { getDailyProfile } from "$api/generated/loopAnalysis.generated.remote";
  import {
    getDailyBasalBolusRatios,
    getInsulinDeliveryStatistics,
  } from "$api/generated/statistics.generated.remote";
  import { requireDateParamsContext } from "$lib/hooks/date-params.svelte";
  import { contextResource } from "$lib/hooks/resource-context.svelte";
  import LoopAnalysisFilterBar from "$lib/components/reports/loop-analysis/LoopAnalysisFilterBar.svelte";
  import DailyProfileTab from "$lib/components/reports/loop-analysis/DailyProfileTab.svelte";
  import {
    dailyProfileRequest,
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

  const request = $derived(
    dailyProfileRequest(
      filter,
      { fromDay: reportsParams.fromDay, toDay: reportsParams.toDay, dayCount: reportsParams.dayCount },
      { limitTo24Hours: page.data.limitTo24Hours ?? false, now: new Date() }
    )
  );

  const profileResource = contextResource(() => getDailyProfile(request.query), {
    errorTitle: "Error Loading Loop Analysis",
  });

  // The strip composes the existing insulin reports over the same instants; their end is inclusive.
  const statisticsDates = $derived({
    startDate: request.primary.from.toISOString(),
    endDate: new Date(request.primary.to.getTime() - 1).toISOString(),
  });
  const insulinResource = contextResource(() => getInsulinDeliveryStatistics(statisticsDates), {
    errorTitle: "Error Loading Loop Analysis",
  });
  const ratiosResource = contextResource(() => getDailyBasalBolusRatios(statisticsDates), {
    errorTitle: "Error Loading Loop Analysis",
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
    primary={request.primary}
    comparison={request.comparison}
    limitedTo24Hours={request.limitedTo24Hours}
    profileNames={profileResource.current?.primary?.profileNames ?? []}
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
      {#if profileResource.current}
        <DailyProfileTab
          response={profileResource.current}
          insulinStats={insulinResource.current}
          ratios={ratiosResource.current}
        />
      {/if}
    </Tabs.Content>
  </Tabs.Root>
</div>
