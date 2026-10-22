<!--
  One curve's bias by local hour of the prediction and horizon, as a table whose cells are tinted
  by the sign and size of the bias: one hue for predictions that ran high, another for low, fading
  to the surface at zero. Every cell carries its value, so the tint is never read alone.
-->
<script lang="ts">
  import type { PredictionCurveAccuracy, PredictionHorizonAccuracy } from "$lib/api";
  import { hourLabel } from "$lib/utils/formatting";
  import { atMinute } from "./daily-profile-view";
  import { curveLabel, horizonLabel } from "./prediction-labels.svelte";

  interface Props {
    curve: PredictionCurveAccuracy;
    formatError: (mgdl: number) => string;
    unit: string;
  }

  let { curve, formatError, unit }: Props = $props();

  /** The bias, in mg/dL, at which a cell takes its full tint. */
  const FULL_TINT_MGDL = 30;
  const MAX_TINT = 55;

  const hours = $derived(curve.byHour ?? []);
  const minutes = $derived((curve.horizons ?? []).map((h) => h.minutes ?? 0));

  function tint(h: PredictionHorizonAccuracy | undefined): string | undefined {
    const bias = h?.error?.mean;
    if (bias == null || bias === 0) return undefined;
    const share = Math.round(Math.min(1, Math.abs(bias) / FULL_TINT_MGDL) * MAX_TINT);
    return `color-mix(in oklab, var(${bias > 0 ? "--bias-high" : "--bias-low"}) ${share}%, transparent)`;
  }

  const cellAt = (hour: number, minute: number) =>
    hours.find((h) => h.hour === hour)?.horizons?.find((h) => h.minutes === minute);
</script>

<div class="bias-heatmap space-y-2">
  <div class="overflow-x-auto">
    <table class="w-full border-separate border-spacing-0.5 text-xs tabular-nums">
      <caption class="sr-only">
        {curveLabel(curve.curve)} bias by hour of day and horizon, in {unit}; positive where the prediction ran high
      </caption>
      <thead>
        <tr>
          <th scope="col" class="px-2 py-1 text-left font-normal text-muted-foreground">Hour</th>
          {#each minutes as minute (minute)}
            <th scope="col" class="px-2 py-1 text-right font-normal text-muted-foreground">{horizonLabel(minute)}</th>
          {/each}
        </tr>
      </thead>
      <tbody>
        {#each hours as hour (hour.hour)}
          <tr>
            <th scope="row" class="px-2 py-0.5 text-left font-normal text-muted-foreground">
              {hourLabel(atMinute((hour.hour ?? 0) * 60))}
            </th>
            {#each minutes as minute (minute)}
              {@const cell = cellAt(hour.hour ?? 0, minute)}
              <td
                class="rounded-sm bg-(--tint) px-2 py-0.5 text-right {cell?.error?.sparse ? 'text-muted-foreground italic' : ''}"
                style:--tint={tint(cell) ?? "transparent"}
              >
                {cell?.error ? formatError(cell.error.mean ?? 0) : "–"}
              </td>
            {/each}
          </tr>
        {/each}
      </tbody>
    </table>
  </div>
  <div class="flex flex-wrap items-center gap-x-4 gap-y-1 px-1 text-xs text-muted-foreground">
    <span class="flex items-center gap-1.5">
      <span class="swatch-low inline-block size-3 rounded-sm"></span>
      Predicted too low
    </span>
    <span class="flex items-center gap-1.5">
      <span class="inline-block size-3 rounded-sm border"></span>
      Near zero
    </span>
    <span class="flex items-center gap-1.5">
      <span class="swatch-high inline-block size-3 rounded-sm"></span>
      Predicted too high
    </span>
    <span class="italic">Italic: too few days to read as a pattern</span>
  </div>
</div>

<style>
  .bias-heatmap {
    --bias-high: #3b7fd1;
    --bias-low: #d04a3c;
  }

  :global(.dark) .bias-heatmap {
    --bias-high: #5b97e0;
    --bias-low: #e0665a;
  }

  .swatch-low {
    background-color: color-mix(in oklab, var(--bias-low) 55%, transparent);
  }

  .swatch-high {
    background-color: color-mix(in oklab, var(--bias-high) 55%, transparent);
  }
</style>
