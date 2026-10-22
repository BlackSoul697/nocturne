<!-- What every Loop Analysis tab says about how its request was read: the history clamp and ignored filters. -->
<script lang="ts">
  import type { LoopAnalysisResponse } from "$lib/api";
  import * as Alert from "$lib/components/ui/alert";
  import Clock from "@lucide/svelte/icons/clock";
  import FilterX from "@lucide/svelte/icons/funnel-x";
  import { ignoredFilterLabel } from "./filter-labels.svelte";

  let { response }: { response: LoopAnalysisResponse } = $props();

  // Under the clamp the range parameters are always ignored; the clamp notice says so instead.
  const RANGE_PARAMETERS = new Set(["startDate", "endDate", "compare", "compareStartDate", "compareEndDate"]);
  const ignoredFilters = $derived(
    (response.ignoredFilters ?? []).filter((name) => !RANGE_PARAMETERS.has(name))
  );
</script>

{#if response.historyClamped}
  <Alert.Root variant="info">
    <Clock />
    <Alert.Title>Only the last 24 hours are included</Alert.Title>
    <Alert.Description>
      You can read only the most recent 24 hours of this data, so the report covers those 24
      hours whatever range is picked, compares nothing, and each time of day rests on at most
      one day.
    </Alert.Description>
  </Alert.Root>
{/if}

{#if ignoredFilters.length > 0}
  <Alert.Root variant="info">
    <FilterX />
    <Alert.Title>Some filters were not applied</Alert.Title>
    <Alert.Description>
      Your access does not include the data these filters are drawn from, so they were ignored:
      {ignoredFilters.map(ignoredFilterLabel).join(", ")}.
    </Alert.Description>
  </Alert.Root>
{/if}
