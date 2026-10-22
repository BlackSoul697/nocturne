<script lang="ts">
  import { getLoopHealth } from "$api/generated/loopAnalysis.generated.remote";
  import { contextResource } from "$lib/hooks/resource-context.svelte";
  import { getLoopAnalysis } from "$lib/components/reports/loop-analysis/loop-analysis-context.svelte";
  import PageBodyLoading from "$lib/components/reports/loop-analysis/PageBodyLoading.svelte";
  import LoopHealthTab from "$lib/components/reports/loop-analysis/LoopHealthTab.svelte";

  const analysis = getLoopAnalysis();
  const resource = contextResource(() => getLoopHealth(analysis.query), { errorTitle: "Error Loading the Loop Health" });
</script>

<svelte:head>
  <title>Loop health - Loop Analysis - Nocturne Reports</title>
  <meta name="description" content="How much of the time the loop was closed, why it stopped and how frequently it reached its limits." />
</svelte:head>

{#if resource.current}
  <LoopHealthTab response={resource.current} />
{:else}
  <PageBodyLoading />
{/if}
