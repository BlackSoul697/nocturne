<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import * as Tabs from "$lib/components/ui/tabs";
  import type { LoopAnalysisResponse } from "$lib/api";
  import {
    getDailyProfile,
    getEventResponse,
    getLoopHealth,
    getPredictionAccuracy,
    getSettingsEvidence,
  } from "$api/generated/loopAnalysis.generated.remote";
  import { requireDateParamsContext } from "$lib/hooks/date-params.svelte";
  import LoopAnalysisFilterBar from "$lib/components/reports/loop-analysis/LoopAnalysisFilterBar.svelte";
  import DailyProfileTab from "$lib/components/reports/loop-analysis/DailyProfileTab.svelte";
  import EventResponseTab from "$lib/components/reports/loop-analysis/EventResponseTab.svelte";
  import EventCohortBuilder from "$lib/components/reports/loop-analysis/EventCohortBuilder.svelte";
  import PredictionsTab from "$lib/components/reports/loop-analysis/PredictionsTab.svelte";
  import LoopHealthTab from "$lib/components/reports/loop-analysis/LoopHealthTab.svelte";
  import SettingsEvidenceTab from "$lib/components/reports/loop-analysis/SettingsEvidenceTab.svelte";
  import TabResource from "$lib/components/reports/loop-analysis/TabResource.svelte";
  import TabLoading from "$lib/components/reports/loop-analysis/TabLoading.svelte";
  import { LoopAnalysisTabState } from "$lib/components/reports/loop-analysis/loop-analysis-tab.svelte";
  import {
    dailyProfileQuery,
    isLoopAnalysisTab,
    LOOP_ANALYSIS_TABS,
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

  /** Each tab, keyed as the URL names it, with the request the filter makes of it. */
  const TABS = {
    "daily-profile": new LoopAnalysisTabState("Daily profile", () => query, getDailyProfile),
    "meals-events": new LoopAnalysisTabState("Meals & events", () => eventResponseQuery(query, events), getEventResponse),
    predictions: new LoopAnalysisTabState("Predictions", () => query, getPredictionAccuracy),
    "loop-health": new LoopAnalysisTabState("Loop health", () => query, getLoopHealth),
    "settings-evidence": new LoopAnalysisTabState("Settings evidence", () => query, getSettingsEvidence),
  } satisfies Record<LoopAnalysisTab, unknown>;

  // Only the tab on screen is fetched; its response answers the filter bar as well. A tab left
  // keeps its response and is not fetched again on return unless the filter changed meanwhile.
  $effect.pre(() => TABS[filter.tab].follow());

  const active = $derived(TABS[filter.tab]);
  const stale = $derived(active.stale);
  const failed = $derived(active.failed);
  const response = $derived<LoopAnalysisResponse | undefined>(active.response);

  const dailyProfile = $derived(TABS["daily-profile"].response);
  const eventResponse = $derived(TABS["meals-events"].response);
  const predictions = $derived(TABS.predictions.response);
  const loopHealth = $derived(TABS["loop-health"].response);
  const settingsEvidence = $derived(TABS["settings-evidence"].response);

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
  /** Loop health's sensitivity ratio by hour, under the same filter. */
  const sensitivityHref = $derived.by(() => {
    const kept = [...page.url.searchParams].filter(([name]) => name !== "tab");
    const query = [["tab", "loop-health"], ...kept].map(([name, value]) => `${name}=${encodeURIComponent(value)}`).join("&");
    return resolve(`/reports/loop-analysis?${query}#sensitivity`);
  });
</script>

<svelte:head>
  <title>Loop Analysis - Nocturne Reports</title>
  <meta
    name="description"
    content="How your automated insulin delivery behaves: what it does at each time of day and around meals and events, how well its predictions match what followed, and how steadily it ran"
  />
</svelte:head>

<!-- The tab on screen mounts at once, so the server renders it with its data; a tab left keeps its own. -->
{#each LOOP_ANALYSIS_TABS as tab (tab)}
  {#if filter.tab === tab || TABS[tab].query !== undefined}
    <TabResource tab={TABS[tab]} active={filter.tab === tab} />
  {/if}
{/each}

<div class="@container space-y-6">
  <div class="space-y-1 text-muted-foreground print:hidden">
    <p>
      How your loop behaves, observed from your data. Every figure comes with its spread and the
      number of days, events or predictions behind it.
    </p>
    <ul class="list-disc space-y-0.5 pl-5 text-sm">
      <li>
        <span class="font-medium text-foreground">Daily profile</span>: glucose, basal, the
        correction the loop added and insulin and carbs on board, at each time of day.
      </li>
      <li>
        <span class="font-medium text-foreground">Meals & events</span>: glucose and insulin lined up
        on meals, corrections, unannounced rises and other events, from before each to after it.
      </li>
      <li>
        <span class="font-medium text-foreground">Predictions</span>: how far each curve the loop
        predicted with landed from the readings that followed.
      </li>
      <li>
        <span class="font-medium text-foreground">Loop health</span>: how much of the time the loop
        was closed, where it stopped, and how often it ran into its limits.
      </li>
      <li>
        <span class="font-medium text-foreground">Settings evidence</span>: the basal, insulin
        sensitivity and carb ratio your data shows beside your profile's, as observations to discuss
        with your care team.
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
    profileNames={response?.profileNames ?? []}
  />

  <Tabs.Root
    value={filter.tab}
    onValueChange={(tab) => {
      if (isLoopAnalysisTab(tab)) setFilter({ ...filter, tab });
    }}
  >
    <Tabs.List class="print:hidden">
      {#each LOOP_ANALYSIS_TABS as tab (tab)}
        <Tabs.Trigger value={tab}>{TABS[tab].label}</Tabs.Trigger>
      {/each}
    </Tabs.List>
    <Tabs.Content value="daily-profile" class="mt-4">
      {#if dailyProfile}
        <DailyProfileTab response={dailyProfile} {insulinDeliveryHref} />
      {:else if !TABS["daily-profile"].failed}
        <TabLoading />
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
      {:else if !TABS["meals-events"].failed}
        <TabLoading />
      {/if}
    </Tabs.Content>
    <Tabs.Content value="predictions" class="mt-4">
      {#if predictions}
        <PredictionsTab response={predictions} />
      {:else if !TABS.predictions.failed}
        <TabLoading />
      {/if}
    </Tabs.Content>
    <Tabs.Content value="loop-health" class="mt-4">
      {#if loopHealth}
        <LoopHealthTab response={loopHealth} />
      {:else if !TABS["loop-health"].failed}
        <TabLoading />
      {/if}
    </Tabs.Content>
    <Tabs.Content value="settings-evidence" class="mt-4">
      {#if settingsEvidence}
        <SettingsEvidenceTab response={settingsEvidence} {sensitivityHref} />
      {:else if !TABS["settings-evidence"].failed}
        <TabLoading />
      {/if}
    </Tabs.Content>
  </Tabs.Root>
</div>
