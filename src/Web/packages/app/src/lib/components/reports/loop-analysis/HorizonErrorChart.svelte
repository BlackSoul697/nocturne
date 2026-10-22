<!--
  One prediction curve's error by horizon, as a slider over its horizons: one tab stop, left and
  right (with Home and End) move between horizons, and the horizon under the pointer or focus is
  read out in a tooltip and as the slider's value.
-->
<script lang="ts">
  import { Axis, Chart, Rule, Svg } from "layerchart";
  import type { PredictionCurveAccuracy, PredictionHorizonAccuracy } from "$lib/api";
  import { plural } from "$lib/utils/plural";
  import HorizonErrorMarks from "./HorizonErrorMarks.svelte";
  import { curveCaption, curveLabel, horizonLabel } from "./prediction-labels.svelte";

  interface Props {
    curve: PredictionCurveAccuracy;
    comparison?: PredictionCurveAccuracy | null;
    color: string;
    yDomain: [number, number];
    scale: (mgdl: number) => number;
    /** A signed glucose difference in display units. */
    formatError: (mgdl: number) => string;
    /** An unsigned glucose difference in display units. */
    formatMae: (mgdl: number) => string;
    unit: string;
    /** Whether the interrupted counts are served; without treatments they are not. */
    showInterrupted: boolean;
  }

  let { curve, comparison = null, color, yDomain, scale, formatError, formatMae, unit, showInterrupted }: Props =
    $props();

  const horizons = $derived(curve.horizons ?? []);
  const compared = $derived(comparison?.horizons ?? null);
  const title = $derived(curveLabel(curve.curve));

  let hovered = $state<number | null>(null);
  let focused = $state(false);
  let current = $state(0);
  const shown = $derived(hovered ?? (focused ? current : null));

  const padding = { top: 18, right: 8, bottom: 22, left: 40 };

  function indexAt(event: PointerEvent, element: HTMLElement): number | null {
    const box = element.getBoundingClientRect();
    const width = box.width - padding.left - padding.right;
    const fraction = (event.clientX - box.left - padding.left) / width;
    if (width <= 0 || fraction < 0 || fraction >= 1) return null;
    return Math.floor(fraction * horizons.length);
  }

  function onkeydown(event: KeyboardEvent) {
    const last = horizons.length - 1;
    const next =
      event.key === "ArrowRight" || event.key === "ArrowUp"
        ? Math.min(last, current + 1)
        : event.key === "ArrowLeft" || event.key === "ArrowDown"
          ? Math.max(0, current - 1)
          : event.key === "Home"
            ? 0
            : event.key === "End"
              ? last
              : null;
    if (next == null) return;
    current = next;
    event.preventDefault();
  }

  const compareAt = (h: PredictionHorizonAccuracy) => compared?.find((c) => c.minutes === h.minutes);

  function describe(h: PredictionHorizonAccuracy | undefined): string {
    if (!h) return "";
    if (!h.error) return `${horizonLabel(h.minutes)}: no matched predictions`;
    return `${horizonLabel(h.minutes)}: bias ${formatError(h.error.mean ?? 0)} ${unit}, MAE ${formatMae(h.mae ?? 0)} ${unit}, ${plural(h.error.n ?? 0, ["# prediction", "# predictions"])}`;
  }

  const tickFormat = (v: number) => horizonLabel(horizons[Math.floor(v)]?.minutes);
</script>

<section class="space-y-1" aria-label={title}>
  <div class="flex items-baseline justify-between gap-2 px-1">
    <h3 class="flex items-center gap-2 text-sm font-medium">
      <span class="inline-block h-0.5 w-3 rounded-full bg-(--curve)" style:--curve={color} aria-hidden="true"></span>
      {title}
    </h3>
    <span class="text-xs text-muted-foreground">{curveCaption(curve.curve)}</span>
  </div>

  <div
    class="relative h-48 w-full touch-pan-y rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
    role="slider"
    tabindex="0"
    aria-label={title}
    aria-valuemin={0}
    aria-valuemax={Math.max(0, horizons.length - 1)}
    aria-valuenow={current}
    aria-valuetext={describe(horizons[current])}
    onpointermove={(event) => (hovered = indexAt(event, event.currentTarget))}
    onpointerleave={() => (hovered = null)}
    onfocus={() => (focused = true)}
    onblur={() => (focused = false)}
    {onkeydown}
  >
    <Chart data={[]} xDomain={[0, Math.max(1, horizons.length)]} {yDomain} {padding}>
      <Svg>
        <Axis
          placement="left"
          ticks={4}
          grid={{ class: "stroke-border/60" }}
          tickLabelProps={{ class: "text-2xs fill-muted-foreground" }}
        />
        <Rule y={0} class="stroke-foreground/50" />
        <HorizonErrorMarks {horizons} comparison={compared} {color} {scale} {formatMae} selected={shown} />
        <Axis
          placement="bottom"
          rule={{ class: "stroke-border" }}
          ticks={horizons.map((_, i) => i + 0.5)}
          format={tickFormat}
          tickLabelProps={{ class: "text-2xs fill-muted-foreground" }}
        />
      </Svg>
    </Chart>

    {#if shown != null && horizons[shown]}
      {@const h = horizons[shown]}
      {@const c = compareAt(h)}
      <div
        class="pointer-events-none absolute top-1 z-10 min-w-48 rounded-md border bg-popover px-3 py-2 text-xs text-popover-foreground shadow-md {shown <
        horizons.length / 2
          ? 'right-2'
          : 'left-12'}"
        role="tooltip"
      >
        <p class="mb-1 font-medium">{title}, {horizonLabel(h.minutes)}</p>
        {#if !h.error}
          <p class="text-muted-foreground">No matched predictions</p>
        {:else}
          <dl class="grid grid-cols-[auto_auto] gap-x-3 tabular-nums">
            <dt class="text-muted-foreground">Bias</dt>
            <dd class="text-right">{formatError(h.error.mean ?? 0)} {unit}</dd>
            <dt class="text-muted-foreground">MAE</dt>
            <dd class="text-right">{formatMae(h.mae ?? 0)} {unit}</dd>
            <dt class="text-muted-foreground">MARD</dt>
            <dd class="text-right">{(h.mard ?? 0).toFixed(1)}%</dd>
            <dt class="text-muted-foreground">P10 to P90</dt>
            <dd class="text-right">{formatError(h.error.p10 ?? 0)} to {formatError(h.error.p90 ?? 0)}</dd>
            <dt class="text-muted-foreground">Matched</dt>
            <dd class="text-right">{h.error.n ?? 0}</dd>
            <dt class="text-muted-foreground">Unmatched</dt>
            <dd class="text-right">{h.unmatched ?? 0}</dd>
            <dt class="text-muted-foreground">Excluded</dt>
            <dd class="text-right">{h.excluded ?? 0}</dd>
            {#if showInterrupted && h.interrupted != null}
              <dt class="text-muted-foreground">Interrupted</dt>
              <dd class="text-right">{h.interrupted}</dd>
            {/if}
            {#if c}
              <dt class="text-muted-foreground">Compared bias</dt>
              <dd class="text-right">{c.error ? `${formatError(c.error.mean ?? 0)} ${unit}` : "–"}</dd>
            {/if}
          </dl>
          {#if h.settingsSignal}
            <p class="mt-1 text-muted-foreground">A settings signal: early enough that the loop has not acted on it yet</p>
          {/if}
          {#if h.error.sparse}
            <p class="mt-1 text-muted-foreground">Too few days to read as a pattern</p>
          {/if}
        {/if}
      </div>
    {/if}
  </div>
</section>
