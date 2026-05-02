<script lang="ts">
  import { Chart, Spline, Axis } from 'layerchart/canvas';
  import { scaleLinear } from 'd3-scale';
  import { curveStepAfter } from 'd3-shape';
  import LaneFrame from './LaneFrame.svelte';
  import { BIN_COUNT, BIN_MINUTES, X_DOMAIN, X_TICKS, formatXTick, useThemeColors } from '../lib/lane-context.svelte';

  const colors = useThemeColors(['--iob-basal']);

  type Props = {
    /** 288-bin scheduled basal in U/h. From the most-recent profile (multi-day) or the day's actual schedule (single-day). */
    bins: ReadonlyArray<number>;
    height?: number;
    showXAxis?: boolean;
  };

  let { bins, height = 80, showXAxis = false }: Props = $props();

  type StepPoint = { minute: number; rate: number };
  let points = $derived<StepPoint[]>(
    bins.length === BIN_COUNT
      ? bins.map((rate, i) => ({ minute: i * BIN_MINUTES, rate }))
      : [],
  );

  let yMax = $derived(Math.max(0.5, ...points.map((p) => p.rate)) * 1.1);
</script>

<LaneFrame title="Basal" {height} {showXAxis}>
  {#snippet chart()}
    <Chart
      xDomain={X_DOMAIN}
      yDomain={[0, yMax]}
      xScale={scaleLinear()}
      yScale={scaleLinear()}
      padding={{ top: 4, right: 6, bottom: showXAxis ? 18 : 4, left: 28 }}
    >
      <Axis
        placement="left"
        ticks={[0, yMax / 2, yMax]}
        format={(v: number) => v.toFixed(1)}
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

      <Spline
        data={points}
        x={(d: StepPoint) => d.minute}
        y={(d: StepPoint) => d.rate}
        curve={curveStepAfter}
        stroke={colors['--iob-basal']}
        strokeWidth={1.5}
      />
    </Chart>
  {/snippet}
</LaneFrame>
