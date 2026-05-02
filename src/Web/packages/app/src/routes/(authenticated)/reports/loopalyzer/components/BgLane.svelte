<script lang="ts">
  import { Chart, Spline, Area, Rect, Axis, Rule } from 'layerchart/canvas';
  import { scaleLinear } from 'd3-scale';
  import LaneFrame from './LaneFrame.svelte';
  import {
    BIN_COUNT,
    BIN_MINUTES,
    X_DOMAIN,
    X_TICKS,
    formatXTick,
  } from '../lib/lane-context.svelte';
  import type { LaneAggregate } from '../lib/aggregate';
  import type {
    LoopalyzerPrediction,
    LoopalyzerMeal,
    LoopalyzerBolus,
    LoopalyzerSiteEvent,
  } from '$lib/api/generated/nocturne-api-client';

  type DaySgv = { date: string; sgv: ReadonlyArray<number | null> };
  type ViewMode = 'average' | 'spaghetti';

  type Props = {
    /** Per-bin avg/P10/P90 across the range. Used in 'average' mode. */
    aggregate: LaneAggregate;
    /** Per-day SGV bin arrays. Used in 'spaghetti' mode. */
    days: ReadonlyArray<DaySgv>;
    viewMode: ViewMode;
    /** ISO date (yyyy-MM-dd) of today's day to highlight in spaghetti mode; null = no highlight. */
    todayDate?: string | null;
    /** Target range in mg/dL (drives the shaded band). Pass null to skip the band. */
    bgLow: number | null;
    bgHigh: number | null;
    /** Single-day-only overlays. Empty arrays in multi-day. */
    predictions?: ReadonlyArray<LoopalyzerPrediction>;
    meals?: ReadonlyArray<LoopalyzerMeal>;
    boluses?: ReadonlyArray<LoopalyzerBolus>;
    siteChanges?: ReadonlyArray<LoopalyzerSiteEvent>;
    sensorChanges?: ReadonlyArray<LoopalyzerSiteEvent>;
    height?: number;
    showXAxis?: boolean;
  };

  let {
    aggregate,
    days,
    viewMode,
    todayDate = null,
    bgLow,
    bgHigh,
    predictions = [],
    meals = [],
    boluses = [],
    siteChanges = [],
    sensorChanges = [],
    height = 280,
    showXAxis = false,
  }: Props = $props();

  const Y_DOMAIN: [number, number] = [40, 300];

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

  type SpaghettiSeries = { date: string; isToday: boolean; points: BinPoint[] };
  let spaghettiSeries = $derived<SpaghettiSeries[]>(
    days.map((d) => ({
      date: d.date,
      isToday: todayDate != null && d.date === todayDate,
      points: binsToPoints(d.sgv),
    })),
  );

  type PredSeries = { startMinute: number; key: 'iob' | 'zt' | 'cob' | 'uam'; color: string; points: BinPoint[] };
  function predToSeries(p: LoopalyzerPrediction): PredSeries[] {
    const start = p.minute ?? 0;
    const make = (arr: number[] | undefined, key: PredSeries['key'], color: string): PredSeries | null => {
      if (!arr || arr.length === 0) return null;
      const points: BinPoint[] = arr.map((v, i) => ({
        minute: start + i * BIN_MINUTES,
        value: v,
      }));
      return { startMinute: start, key, color, points };
    };
    return [
      make(p.iob, 'iob', 'var(--chart-1)'),
      make(p.zt, 'zt', 'var(--chart-5)'),
      make(p.cob, 'cob', 'var(--chart-3)'),
      make(p.uam, 'uam', 'var(--chart-4)'),
    ].filter((s): s is PredSeries => s != null);
  }
  let predictionSeries = $derived(predictions.flatMap(predToSeries));

  let hasTargetBand = $derived(bgLow != null && bgHigh != null);
</script>

<LaneFrame title="BG" {height} {showXAxis}>
  {#snippet chart()}
    <Chart
      xDomain={X_DOMAIN}
      yDomain={Y_DOMAIN}
      xScale={scaleLinear()}
      yScale={scaleLinear()}
      padding={{ top: 6, right: 6, bottom: showXAxis ? 18 : 4, left: 28 }}
    >
      {#if hasTargetBand}
        <Rect
          x={0}
          width={1440}
          y0={bgLow ?? 70}
          y1={bgHigh ?? 180}
          fill="var(--chart-2)"
          fillOpacity={0.12}
        />
      {/if}

      <Axis
        placement="left"
        ticks={[40, 80, 180, 300]}
        format={(v: number) => String(v)}
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
          fill="var(--chart-1)"
          fillOpacity={0.18}
          line={false}
        />
        <Spline
          data={avgPoints}
          x={(d: BinPoint) => d.minute}
          y={(d: BinPoint) => d.value ?? 0}
          defined={(d: BinPoint) => d.value != null}
          stroke="var(--chart-1)"
          strokeWidth={2}
        />
      {:else}
        {#each spaghettiSeries as series (series.date)}
          <Spline
            data={series.points}
            x={(d: BinPoint) => d.minute}
            y={(d: BinPoint) => d.value ?? 0}
            defined={(d: BinPoint) => d.value != null}
            stroke="var(--chart-1)"
            strokeWidth={series.isToday ? 1.75 : 1}
            strokeOpacity={series.isToday ? 0.9 : 0.22}
          />
        {/each}
      {/if}

      {#each predictionSeries as series, i (i)}
        <Spline
          data={series.points}
          x={(d: BinPoint) => d.minute}
          y={(d: BinPoint) => d.value ?? 0}
          defined={(d: BinPoint) => d.value != null}
          stroke={series.color}
          strokeWidth={1}
          strokeOpacity={0.6}
        />
      {/each}

      {#each meals as m, i (i)}
        <Rule x={m.minute ?? 0} stroke="var(--chart-3)" strokeWidth={1} strokeOpacity={0.7} />
      {/each}
      {#each boluses as b, i (i)}
        <Rule x={b.minute ?? 0} stroke="var(--chart-1)" strokeWidth={1} strokeOpacity={0.7} />
      {/each}
      {#each siteChanges as s, i (i)}
        <Rule
          x={s.minute ?? 0}
          stroke="var(--muted-foreground)"
          strokeWidth={1}
          strokeOpacity={0.5}
          dashArray={[2, 2]}
        />
      {/each}
      {#each sensorChanges as s, i (i)}
        <Rule
          x={s.minute ?? 0}
          stroke="var(--muted-foreground)"
          strokeWidth={1}
          strokeOpacity={0.5}
          dashArray={[2, 2]}
        />
      {/each}
    </Chart>
  {/snippet}
</LaneFrame>
