<!--
  A value's spread for each hour of the day on the slot axis: the P25-P75 band as a box over the
  hour's twelve slots and the median across it. A sparse hour is faded; the comparison is a dashed
  outline of its box and median.
-->
<script lang="ts">
  import { getChartContext } from "layerchart";
  import type { LoopHealthHourValue } from "$lib/api";
  import { SLOTS_PER_HOUR } from "./loop-health-view";

  interface Props {
    hours: LoopHealthHourValue[];
    comparison?: LoopHealthHourValue[];
    color: string;
  }

  let { hours, comparison = [], color }: Props = $props();

  const ctx = getChartContext();
  const xScale = $derived(ctx.xScale);
  const yScale = $derived(ctx.yScale);
  const GAP = 2;

  function shape(hour: LoopHealthHourValue) {
    const value = hour.value!;
    const x = xScale((hour.hour ?? 0) * SLOTS_PER_HOUR) + GAP / 2;
    const width = Math.max(1, xScale(((hour.hour ?? 0) + 1) * SLOTS_PER_HOUR) - GAP / 2 - x);
    const top = yScale(value.p75 ?? value.p50 ?? 0);
    const bottom = yScale(value.p25 ?? value.p50 ?? 0);
    return { x, width, top, height: Math.max(1, bottom - top), median: yScale(value.p50 ?? 0) };
  }

  const drawn = (list: LoopHealthHourValue[]) => list.filter((h) => h.value != null);
</script>

{#each drawn(hours) as hour (hour.hour)}
  {@const s = shape(hour)}
  {@const opacity = hour.value?.sparse ? 0.35 : 1}
  <rect x={s.x} width={s.width} y={s.top} height={s.height} rx="1" fill={color} fill-opacity={0.3 * opacity} />
  <line x1={s.x} x2={s.x + s.width} y1={s.median} y2={s.median} stroke={color} stroke-opacity={opacity} stroke-width="2" />
{/each}
{#each drawn(comparison) as hour (`c${hour.hour}`)}
  {@const s = shape(hour)}
  <rect
    x={s.x}
    width={s.width}
    y={s.top}
    height={s.height}
    rx="1"
    fill="none"
    stroke="var(--foreground)"
    stroke-opacity="0.85"
    stroke-width="1.25"
    stroke-dasharray="3 2"
  />
  <line x1={s.x} x2={s.x + s.width} y1={s.median} y2={s.median} stroke="var(--foreground)" stroke-width="1.25" stroke-dasharray="3 2" />
{/each}
