<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import EventCohortBuilder from "$lib/components/reports/loop-analysis/EventCohortBuilder.svelte";
  import { loopAnalysisPath } from "$lib/components/reports/loop-analysis/loop-analysis-pages.svelte";
  import {
    eventResponseQuery,
    readEventResponse,
    withoutUnavailableEvents,
    writeEventResponse,
    type EventResponseState,
  } from "$lib/components/reports/loop-analysis/event-response-filter";
  import { getEventResponse } from "$api/generated/loopAnalysis.generated.remote";
  import { contextResource } from "$lib/hooks/resource-context.svelte";
  import { getLoopAnalysis } from "$lib/components/reports/loop-analysis/loop-analysis-context.svelte";
  import PageBodyLoading from "$lib/components/reports/loop-analysis/PageBodyLoading.svelte";
  import EventResponseBody from "$lib/components/reports/loop-analysis/EventResponseBody.svelte";

  const analysis = getLoopAnalysis();
  const events = $derived(readEventResponse(page.url.searchParams));
  const resource = contextResource(() => getEventResponse(eventResponseQuery(analysis.query, events)), { errorTitle: "Cannot load the meals and events" });

  const setEvents = (next: EventResponseState) =>
    // eslint-disable-next-line svelte/no-navigation-without-resolve -- loopAnalysisPath resolves the path
    goto(loopAnalysisPath("meals-and-events", writeEventResponse(next, page.url.searchParams)), {
      replaceState: true,
      noScroll: true,
      keepFocus: true,
    });

  // Event kinds and filters this viewer cannot use leave the URL once the API has named them.
  $effect(() => {
    const response = resource.current;
    if (!response) return;
    const unavailable = response.unavailableFilters?.map((f) => f.filter ?? "") ?? [];
    const next = withoutUnavailableEvents(events, response.unavailableAnchors ?? [], unavailable);
    if (next) void setEvents(next);
  });
</script>

<svelte:head>
  <title>Meals and events - Loop Analysis - Nocturne Reports</title>
  <meta name="description" content="Glucose and insulin before and after meals, corrections and other events." />
</svelte:head>

{#if resource.current}
  <div class="space-y-6">
    <EventCohortBuilder
      value={events}
      onchange={setEvents}
      applied={resource.current.definition}
      unavailableAnchors={resource.current.unavailableAnchors}
      unavailableFilters={resource.current.unavailableFilters}
    />
    <EventResponseBody response={resource.current} />
  </div>
{:else}
  <PageBodyLoading />
{/if}
