<!-- Each figure's spread across each cohort's events, cohorts side by side, the comparison beside each. -->
<script lang="ts">
  import { EventAnchorKind, type EventCohortDefinition, type EventCohortResult } from "$lib/api";
  import { plural } from "$lib/utils/plural";
  import CohortDot from "./CohortDot.svelte";
  import { EVENT_METRICS, METRIC_KINDS, type EventMetricKey } from "./event-response-view";
  import { anchorLabel, formatMetric, metricLabels } from "./event-labels.svelte";

  interface Props {
    cohorts: EventCohortDefinition[];
    primary: EventCohortResult[];
    comparison?: EventCohortResult[] | null;
    /** Figures the caller's access withholds, left out of the table. */
    hidden: (metric: EventMetricKey) => boolean;
  }

  let { cohorts, primary, comparison = null, hidden }: Props = $props();

  const labels = $derived(metricLabels());
  const anyMeal = $derived(cohorts.some((c) => c.anchor === EventAnchorKind.Meal));
  const rows = $derived(
    EVENT_METRICS.filter((m) => !hidden(m) && (m !== "gramsPerUnit" || anyMeal))
  );
  const result = (list: EventCohortResult[] | null | undefined, cohort: number | undefined) =>
    list?.find((r) => r.cohort === cohort);
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
              {anchorLabel(cohort.anchor)}
            </span>
            <span class="block font-normal">
              {plural(result(primary, cohort.cohort)?.events ?? 0, ["# event", "# events"])}
            </span>
          </th>
        {/each}
      </tr>
    </thead>
    <tbody>
      {#each rows as metric (metric)}
        <tr class="border-b last:border-0">
          <th scope="row" class="px-3 py-2 text-left font-normal text-muted-foreground">{labels[metric]}</th>
          {#each cohorts as cohort (cohort.cohort)}
            {@const value = result(primary, cohort.cohort)?.metrics?.[metric]}
            {@const compared = result(comparison, cohort.cohort)?.metrics?.[metric]}
            {@const kind = METRIC_KINDS[metric]}
            <td class="px-3 py-2 text-right {value?.sparse ? 'text-muted-foreground' : ''}">
              {#if value}
                <span class="font-medium">{formatMetric(kind, value.p50)}</span>
                <span class="block text-xs text-muted-foreground">
                  {formatMetric(kind, value.p25)} to {formatMetric(kind, value.p75)}, n {value.n}
                </span>
              {:else}
                –
              {/if}
              {#if comparison}
                <span class="block text-xs text-muted-foreground">
                  Compared {compared ? formatMetric(kind, compared.p50) : "–"}
                </span>
              {/if}
            </td>
          {/each}
        </tr>
      {/each}
    </tbody>
  </table>
</div>
<p class="px-1 text-xs text-muted-foreground">
  Medians, with the middle half of events below each. Faded figures rest on too few events to read
  as a pattern. These are observations of what happened, not settings to change.
</p>
