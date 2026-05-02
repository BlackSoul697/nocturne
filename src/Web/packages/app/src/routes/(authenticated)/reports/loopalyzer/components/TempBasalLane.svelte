<script lang="ts">
  import { Chart, Spline, Axis } from 'layerchart/canvas';
  import { scaleLinear } from 'd3-scale';
  import { curveStepAfter } from 'd3-shape';
  import LaneFrame from './LaneFrame.svelte';
  import { BIN_COUNT, BIN_MINUTES, X_DOMAIN, X_TICKS, formatXTick, useThemeColors } from '../lib/lane-context.svelte';

  const colors = useThemeColors(['--iob-temporary', '--muted-foreground']);

  type Props = {
    /** 288-bin actual delivered basal (temp where active, null where no temp). */
    tempBins: ReadonlyArray<number | null>;
    /** 288-bin scheduled basal — drawn faintly behind the temp line as reference. */
    scheduledBins: ReadonlyArray<number>;
    height?: number;
    showXAxis?: boolean;
  };

  let { tempBins, scheduledBins, height = 80, showXAxis = false }: Props = $props();

  type TempPoint = { minute: number; rate: number | null };
  type SchedPoint = { minute: number; rate: number };

  let tempPoints = $derived<TempPoint[]>(
    tempBins.length === BIN_COUNT
      ? tempBins.map((rate, i) => ({ minute: i * BIN_MINUTES, rate: rate ?? null }))
      : [],
  );
  let scheduledPoints = $derived<SchedPoint[]>(
    scheduledBins.length === BIN_COUNT
      ? scheduledBins.map((rate, i) => ({ minute: i * BIN_MINUTES, rate }))
      : [],
  );

  let yMax = $derived(
    Math.max(
      0.5,
      ...scheduledPoints.map((p) => p.rate),
      ...tempPoints.map((p) => p.rate ?? 0),
    ) * 1.1,
  );
</script>

<LaneFrame title="Temp" {height} {showXAxis}>
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
        data={scheduledPoints}
        x={(d: SchedPoint) => d.minute}
        y={(d: SchedPoint) => d.rate}
        curve={curveStepAfter}
        stroke={colors['--muted-foreground']}
        strokeWidth={1}
        strokeOpacity={0.35}
      />
      <Spline
        data={tempPoints}
        x={(d: TempPoint) => d.minute}
        y={(d: TempPoint) => d.rate ?? 0}
        defined={(d: TempPoint) => d.rate != null}
        curve={curveStepAfter}
        stroke={colors['--iob-temporary']}
        strokeWidth={1.5}
      />
    </Chart>
  {/snippet}
</LaneFrame>
