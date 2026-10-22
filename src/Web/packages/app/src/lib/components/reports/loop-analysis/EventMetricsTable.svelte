<!-- Each figure's spread across each cohort's events, cohorts side by side, the comparison beside each. -->
<script lang="ts">
  import type { EventCohortDefinition, EventCohortResult } from "$lib/api";
  import { plural } from "$lib/utils/plural";
  import CohortDot from "./CohortDot.svelte";
  import { cohortLabel, formatMetric, metricDefinitions } from "./event-labels.svelte";

  interface Props {
    cohorts: EventCohortDefinition[];
    primary: EventCohortResult[];
    comparison?: EventCohortResult[] | null;
  }

  let { cohorts, primary, comparison = null }: Props = $props();

  const result = (list: EventCohortResult[] | null | undefined, cohort: number | undefined) =>
    list?.find((r) => r.cohort === cohort);

  // A figure the response carries for no cohort, withheld or never observed, is left out.
  const rows = $derived(
    metricDefinitions().filter((m) =>
      [...primary, ...(comparison ?? [])].some((r) => r.metrics?.[m.key] != null)
    )
  );
  const missingGramsPerUnit = $derived(
    primary.filter(
      (r) => (r.gramsPerUnitNotIsolated ?? 0) + (r.gramsPerUnitNoNetInsulin ?? 0) + (r.gramsPerUnitUncovered ?? 0) > 0
    )
  );
</script>

<div class="overflow-x-auto rounded-lg border bg-card">
  <table class="w-full text-sm tabular-nums">
    <caption class="sr-only">Each figure's median and middle half across each cohort's events</caption>
    <thead>
      <tr class="border-b text-xs text-muted-foreground">
        <th scope="col" class="px-3 py-2 text-left font-medium">Observed</th>
        {#each cohorts as cohort, i (cohort.cohort)}
          <th scope="col" class="px-3 py-2 text-right font-medium">
            <span class="inline-flex items-center gap-1.5">
              <CohortDot index={i} />
              {cohortLabel(cohort)}
            </span>
            <span class="block font-normal">
              {plural(result(primary, cohort.cohort)?.events ?? 0, ["# event", "# events"])}
            </span>
          </th>
        {/each}
      </tr>
    </thead>
    <tbody>
      {#each rows as metric (metric.key)}
        <tr class="border-b last:border-0">
          <th scope="row" class="px-3 py-2 text-left font-normal text-muted-foreground">{metric.label}</th>
          {#each cohorts as cohort (cohort.cohort)}
            {@const value = result(primary, cohort.cohort)?.metrics?.[metric.key]}
            {@const compared = result(comparison, cohort.cohort)?.metrics?.[metric.key]}
            {@const events = result(primary, cohort.cohort)?.events ?? 0}
            <td class="px-3 py-2 text-right {value?.sparse ? 'text-muted-foreground' : ''}">
              {#if value}
                <span class="font-medium">{formatMetric(metric.kind, value.p50)}</span>
                <span class="block text-xs text-muted-foreground">
                  {formatMetric(metric.kind, value.p25)} to {formatMetric(metric.kind, value.p75)}, n {value.n}
                </span>
              {:else}
                –
              {/if}
              {#if metric.key === "minutesBackToStart"}
                <span class="block text-xs text-muted-foreground">
                  {value?.n ?? 0} of {plural(events, ["# event", "# events"])} came back
                </span>
              {/if}
              {#if comparison}
                <span class="block text-xs text-muted-foreground">
                  Compared {compared ? formatMetric(metric.kind, compared.p50) : "–"}
                </span>
              {/if}
            </td>
          {/each}
        </tr>
      {/each}
    </tbody>
  </table>
</div>
<div class="space-y-1 px-1 text-xs text-muted-foreground">
  <p>
    Medians, with the middle half of events below each. Faded figures rest on too few events to read
    as a pattern. These are observations of what happened, not settings to change.
  </p>
  {#each missingGramsPerUnit as r (r.cohort)}
    {@const cohort = cohorts.find((c) => c.cohort === r.cohort)}
    <p>
      {cohort ? cohortLabel(cohort) : ""}: grams per unit is read only off a meal with no other meal,
      correction or rise in its window, and whose window's insulin the pump's records account for.
      {plural(r.gramsPerUnitNotIsolated ?? 0, ["# meal had another intake", "# meals had another intake"])};
      {plural(r.gramsPerUnitUncovered ?? 0, [
        "# had too little of its basal recorded",
        "# had too little of their basal recorded",
      ])}; {plural(r.gramsPerUnitNoNetInsulin ?? 0, [
        "# had no insulin above schedule",
        "# had no insulin above schedule",
      ])}.
    </p>
  {/each}
</div>
