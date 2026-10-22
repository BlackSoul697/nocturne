<!-- One curve's accuracy in each context it is reported in, by horizon. -->
<script lang="ts">
  import type { PredictionCurveAccuracy } from "$lib/api";
  import { contextCaption, contextLabel, curveLabel, horizonLabel } from "./prediction-labels.svelte";

  interface Props {
    curve: PredictionCurveAccuracy;
    formatError: (mgdl: number) => string;
    formatMae: (mgdl: number) => string;
    unit: string;
  }

  let { curve, formatError, formatMae, unit }: Props = $props();

  const minutes = $derived((curve.horizons ?? []).map((h) => h.minutes ?? 0));
</script>

<div class="overflow-x-auto">
  <table class="w-full text-xs tabular-nums">
    <caption class="sr-only">{curveLabel(curve.curve)} accuracy by context and horizon, in {unit}</caption>
    <thead>
      <tr class="border-b">
        <th scope="col" class="px-2 py-1.5 text-left font-normal text-muted-foreground">Context</th>
        {#each minutes as minute (minute)}
          <th scope="col" class="px-2 py-1.5 text-right font-normal text-muted-foreground">{horizonLabel(minute)}</th>
        {/each}
      </tr>
    </thead>
    <tbody>
      {#each curve.byContext ?? [] as row (row.context)}
        <tr class="border-b last:border-0">
          <th scope="row" class="px-2 py-1.5 text-left align-top font-normal">
            <span class="block font-medium">{contextLabel(row.context)}</span>
            <span class="block text-muted-foreground">{contextCaption(row.context)}</span>
          </th>
          {#each minutes as minute (minute)}
            {@const h = row.horizons?.find((x) => x.minutes === minute)}
            <td class="px-2 py-1.5 text-right align-top {h?.error?.sparse ? 'text-muted-foreground' : ''}">
              {#if h?.error}
                <span class="block font-medium">Bias {formatError(h.error.mean ?? 0)}</span>
                <span class="block">MAE {formatMae(h.mae ?? 0)}</span>
                <span class="block text-muted-foreground">n {h.error.n ?? 0}</span>
              {:else}
                <span class="block text-muted-foreground">No matched predictions</span>
              {/if}
              <span class="block text-muted-foreground">
                Unmatched {h?.unmatched ?? 0}, excluded {h?.excluded ?? 0}{#if h?.interrupted != null}, interrupted {h.interrupted}{/if}
              </span>
            </td>
          {/each}
        </tr>
      {/each}
    </tbody>
  </table>
</div>
