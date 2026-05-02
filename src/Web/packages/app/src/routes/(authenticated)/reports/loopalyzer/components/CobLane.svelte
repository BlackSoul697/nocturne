<script lang="ts">
  import { Chart, Spline, Area, Axis } from 'layerchart/canvas';
  import { scaleLinear } from 'd3-scale';
  import LaneFrame from './LaneFrame.svelte';
  import {
    BIN_COUNT,
    BIN_MINUTES,
    X_DOMAIN,
    X_TICKS,
    formatXTick,
    useThemeColors,
  } from '../lib/lane-context.svelte';
  import type { LaneAggregate } from '../lib/aggregate';
  import MealAlignmentBand from './MealAlignmentBand.svelte';

  type DayBins = { date: string; bins: ReadonlyArray<number | null> };
  type ViewMode = 'average' | 'spaghetti';

  type Props = {
    aggregate: LaneAggregate;
    days: ReadonlyArray<DayBins>;
    viewMode: ViewMode;
    todayDate?: string | null;
    height?: number;
    showXAxis?: boolean;
  };

  let {
    aggregate,
    days,
    viewMode,
    todayDate = null,
    height = 110,
    showXAxis = false,
  }: Props = $props();

  const colors = useThemeColors(['--chart-3']);

  type BinPoint = { minute: number; value: number | null };
  type BandPoint = { minute: number; p10: number | null; p90: number | null };

  function binsToPoints(bins: ReadonlyArray<number | null>): BinPoint[] {
    const out: BinPoint[] = new Array(BIN_COUNT);
    for (let i = 0; i < BIN_COUNT; i++) {
      out[i] = { minute: i * BIN_MINUTES + BIN_MINUTES / 2, value: bins[i] ?? null };
    }
    return out;
  }

  let avgPoints = $derived(binsToPoints(aggregate.avg));
  let bandPoints = $derived<BandPoint[]>(
    aggregate.avg.map((_, i) => ({
      minute: i * BIN_MINUTES + BIN_MINUTES / 2,
      p10: aggregate.p10[i],
      p90: aggregate.p90[i],
    })),
  );

  let spaghetti = $derived(
    days.map((d) => ({ date: d.date, isToday: todayDate != null && d.date === todayDate, points: binsToPoints(d.bins) })),
  );

  let yMax = $derived(() => {
    let m = 0;
    for (const v of aggregate.p90) if (v != null && v > m) m = v;
    for (const day of days) for (const v of day.bins) if (v != null && v > m) m = v;
    return Math.max(10, m * 1.1);
  });
</script>

<LaneFrame title="COB" {height} {showXAxis}>
  {#snippet chart()}
    <Chart
      xDomain={X_DOMAIN}
      yDomain={[0, yMax()]}
      xScale={scaleLinear()}
      yScale={scaleLinear()}
      padding={{ top: 4, right: 6, bottom: showXAxis ? 18 : 4, left: 28 }}
    >
      <MealAlignmentBand />

      <Axis
        placement="left"
        ticks={[0, yMax() / 2, yMax()]}
        format={(v: number) => v.toFixed(0)}
        tickLength={2}
        rule={false}
      />
      {#if showXAxis}
        <Axis
          placement="bottom"
          ticks={X_TICKS}
          format={(v: number) => formatXTick(v)}
          tickLength={3}
          rule={false}
        />
      {/if}

      {#if viewMode === 'average'}
        <Area
          data={bandPoints}
          x={(d: BandPoint) => d.minute}
          y0={(d: BandPoint) => d.p10 ?? 0}
          y1={(d: BandPoint) => d.p90 ?? 0}
          defined={(d: BandPoint) => d.p10 != null && d.p90 != null}
          fill={colors['--chart-3']}
          fillOpacity={0.18}
          line={false}
        />
        <Spline
          data={avgPoints}
          x={(d: BinPoint) => d.minute}
          y={(d: BinPoint) => d.value ?? 0}
          defined={(d: BinPoint) => d.value != null}
          stroke={colors['--chart-3']}
          strokeWidth={1.5}
        />
      {:else}
        {#each spaghetti as series (series.date)}
          <Spline
            data={series.points}
            x={(d: BinPoint) => d.minute}
            y={(d: BinPoint) => d.value ?? 0}
            defined={(d: BinPoint) => d.value != null}
            stroke={colors['--chart-3']}
            strokeWidth={series.isToday ? 1.5 : 1}
            strokeOpacity={series.isToday ? 0.9 : 0.22}
          />
        {/each}
      {/if}
    </Chart>
  {/snippet}
</LaneFrame>
