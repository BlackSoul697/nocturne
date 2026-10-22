<script lang="ts">
  import { getPredictionAccuracy } from "$api/generated/loopAnalysis.generated.remote";
  import { contextResource } from "$lib/hooks/resource-context.svelte";
  import { getLoopAnalysis } from "$lib/components/reports/loop-analysis/loop-analysis-context.svelte";
  import PageBodyLoading from "$lib/components/reports/loop-analysis/PageBodyLoading.svelte";
  import PredictionsTab from "$lib/components/reports/loop-analysis/PredictionsTab.svelte";

  const analysis = getLoopAnalysis();
  const resource = contextResource(() => getPredictionAccuracy(analysis.query), { errorTitle: "Error Loading the Prediction Accuracy" });
</script>

<svelte:head>
  <title>Prediction accuracy - Loop Analysis - Nocturne Reports</title>
  <meta name="description" content="The glucose predictions of the loop compared with the glucose readings that followed them." />
</svelte:head>

{#if resource.current}
  <PredictionsTab response={resource.current} />
{:else}
  <PageBodyLoading />
{/if}
