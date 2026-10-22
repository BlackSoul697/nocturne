<!--
  A Loop Analysis page inside its layout, with stand-ins for what the reports layout shows of it:
  the page switcher in the header and the Filters panel.
-->
<script lang="ts">
  import type { Component } from "svelte";
  import { createReportSlots } from "$lib/hooks/report-slots.svelte";
  import ReportPageSwitcher from "$lib/components/reports/ReportPageSwitcher.svelte";
  import Layout from "./+layout.svelte";

  let { report }: { report: Component } = $props();
  const Report = $derived(report);

  const slots = createReportSlots();
</script>

<header><ReportPageSwitcher /></header>

<Layout>
  <Report />
</Layout>

{#if slots.filters}
  <section aria-label="Filters panel">
    <button type="button" onclick={() => slots.filters?.begin()}>Open filters</button>
    {@render slots.filters.fields()}
    <button type="button" onclick={() => slots.filters?.apply()}>Apply filters</button>
    <button type="button" onclick={() => slots.filters?.reset()}>Reset filters</button>
  </section>
{/if}
