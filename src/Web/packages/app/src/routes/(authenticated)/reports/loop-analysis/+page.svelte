<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import * as Tabs from "$lib/components/ui/tabs";
  import type { DailyProfileResponse, EventResponseReport, LoopAnalysisResponse } from "$lib/api";
  import { getDailyProfile, getEventResponse } from "$api/generated/loopAnalysis.generated.remote";
  import { requireDateParamsContext } from "$lib/hooks/date-params.svelte";
  import LoopAnalysisFilterBar from "$lib/components/reports/loop-analysis/LoopAnalysisFilterBar.svelte";
  import DailyProfileTab from "$lib/components/reports/loop-analysis/DailyProfileTab.svelte";
  import EventResponseTab from "$lib/components/reports/loop-analysis/EventResponseTab.svelte";
  import EventCohortBuilder from "$lib/components/reports/loop-analysis/EventCohortBuilder.svelte";
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
  import {
    eventResponseQuery,
    readEventResponse,
    withoutUnavailableEvents,
    writeEventResponse,
    type EventResponseState,
  } from "$lib/components/reports/loop-analysis/event-response-filter";

  const reportsParams = requireDateParamsContext(14);

  const filter = $derived(readLoopAnalysisFilter(page.url.searchParams));
  const events = $derived(readEventResponse(page.url.searchParams));

  function navigate(search: URLSearchParams) {
    void goto(resolve(`/reports/loop-analysis?${search.toString()}`), {
      replaceState: true,
      noScroll: true,
      keepFocus: true,
    });
  }

  const setFilter = (next: LoopAnalysisFilterState) =>
    navigate(writeLoopAnalysisFilter(next, page.url.searchParams));
  const setEvents = (next: EventResponseState) => navigate(writeEventResponse(next, page.url.searchParams));

  const requested = $derived({ startDate: reportsParams.fromDay, endDate: reportsParams.toDay });
  const query = $derived(dailyProfileQuery(filter, requested));

  // Only the tab on screen is fetched; its response answers the filter bar as well.
  let dailyProfile = $state<DailyProfileResponse>();
  let eventResponse = $state<EventResponseReport>();
  let stale = $state(false);
  let failed = $state(false);

  const response = $derived<LoopAnalysisResponse | undefined>(
    filter.tab === "meals-events" ? eventResponse : dailyProfile
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
  $effect(() => {
    if (filter.tab !== "meals-events" || !eventResponse) return;
    const unavailable = eventResponse.unavailableFilters?.map((f) => f.filter ?? "") ?? [];
    const next = withoutUnavailableEvents(events, eventResponse.unavailableAnchors ?? [], unavailable);
    if (next) setEvents(next);
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
    { value: "meals-events", label: "Meals & events" },
  ]);
</script>

<svelte:head>
  <title>Loop Analysis - Nocturne Reports</title>
  <meta
    name="description"
    content="How your automated insulin delivery behaves across the day: glucose, basal, loop corrections, IOB and COB by time of day"
  />
</svelte:head>

{#if filter.tab === "meals-events"}
  <TabResource
    load={() => getEventResponse(eventResponseQuery(query, events))}
    onupdate={(state) => {
      eventResponse = state.current;
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
    What your loop did at each time of day and around each meal and event: glucose, scheduled
    against delivered basal, the correction the algorithm added on top, and insulin and carbs on
    board. Every figure is observed from your data, with its spread and the number of days or events
    behind it.
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
    <Tabs.Content value="meals-events" class="mt-4 space-y-6">
      <EventCohortBuilder
        value={events}
        onchange={setEvents}
        applied={eventResponse?.definition}
        unavailableAnchors={eventResponse?.unavailableAnchors}
        unavailableFilters={eventResponse?.unavailableFilters}
      />
      {#if eventResponse}
        <EventResponseTab response={eventResponse} />
      {/if}
    </Tabs.Content>
  </Tabs.Root>
</div>
