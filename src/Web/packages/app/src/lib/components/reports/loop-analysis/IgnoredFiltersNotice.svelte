<!--
  Names the filters a response ignored because the caller's access does not include the data they
  are drawn from. The range parameters are left out: under the history clamp they are always
  ignored, and the clamp notice says so instead.
-->
<script lang="ts">
  import * as Alert from "$lib/components/ui/alert";
  import FilterX from "@lucide/svelte/icons/funnel-x";
  import { ignoredFilterLabel } from "./filter-labels.svelte";

  let { ignored }: { ignored: string[] | undefined } = $props();

  const RANGE_PARAMETERS = new Set(["startDate", "endDate", "compare", "compareStartDate", "compareEndDate"]);
  const filters = $derived((ignored ?? []).filter((name) => !RANGE_PARAMETERS.has(name)));
</script>

{#if filters.length > 0}
  <Alert.Root variant="info">
    <FilterX />
    <Alert.Title>Some filters were not applied</Alert.Title>
    <Alert.Description>
      Your access does not include the data for these filters, so the report did not apply them:
      {filters.map(ignoredFilterLabel).join(", ")}.
    </Alert.Description>
  </Alert.Root>
{/if}
