<!--
  An aggregate series as P10-P90 and P25-P75 bands under a centre line, drawn run by run so a gap
  stays a gap and a sparse run is faded. The comparison range sits underneath, muted.
-->
<script lang="ts">
  import { Area, Spline } from "layerchart";
  import type { DailyProfileSlot, LoopAnalysisAggregate } from "$lib/api";
  import { patternClass } from "$lib/components/charts/print/chart-print-patterns";
  import { aggregateRuns, type AggregatePoint } from "./daily-profile-view";

  interface Props {
    slots: DailyProfileSlot[];
    comparison?: DailyProfileSlot[] | null;
    pick: (slot: DailyProfileSlot) => LoopAnalysisAggregate | undefined;
    /** Display-unit conversion applied to every value before it is drawn. */
    scale?: (value: number) => number;
    outerColor: string;
    innerColor: string;
    lineColor?: string;
    /** Draw the P10-P90 band as well as P25-P75. */
    outer?: boolean;
    /** The centre line: the median, or the mean where the mean is the headline. */
    centre?: "p50" | "mean" | null;
  }

  let {
    slots,
    comparison = null,
    pick,
    scale = (v) => v,
    outerColor,
    innerColor,
    lineColor = "currentColor",
    outer = true,
    centre = "p50",
  }: Props = $props();

  const runs = $derived(aggregateRuns(slots, pick));
  const comparisonRuns = $derived(comparison ? aggregateRuns(comparison, pick) : []);

  const x = (p: AggregatePoint) => p.slot + 0.5;
  const at = (key: keyof LoopAnalysisAggregate) => (p: AggregatePoint) =>
    scale(Number(p.value[key] ?? 0));

  const COMPARISON_FILL = "color-mix(in oklab, var(--muted-foreground) 22%, transparent)";
  const COMPARISON_LINE = "var(--muted-foreground)";
  const SPARSE_OPACITY = 0.35;
</script>

{#each comparisonRuns as run, i (`c${i}`)}
  <Area data={run.points} {x} y0={at("p25")} y1={at("p75")} fill={COMPARISON_FILL} />
  {#if centre}
    <Spline
      data={run.points}
      {x}
      y={at(centre)}
      stroke={COMPARISON_LINE}
      stroke-width={1.5}
      stroke-dasharray="4 3"
      fill="none"
    />
  {/if}
{/each}

{#each runs as run, i (`p${i}`)}
  {@const opacity = run.sparse ? SPARSE_OPACITY : 1}
  {#if outer}
    <Area
      data={run.points}
      {x}
      y0={at("p10")}
      y1={at("p90")}
      fill={outerColor}
      fill-opacity={opacity}
      class={patternClass("percentile-outer")}
    />
  {/if}
  <Area
    data={run.points}
    {x}
    y0={at("p25")}
    y1={at("p75")}
    fill={innerColor}
    fill-opacity={opacity}
    class={patternClass("percentile-inner")}
  />
  {#if centre}
    <Spline
      data={run.points}
      {x}
      y={at(centre)}
      stroke={lineColor}
      stroke-width={2}
      stroke-opacity={opacity}
      fill="none"
    />
  {/if}
{/each}
