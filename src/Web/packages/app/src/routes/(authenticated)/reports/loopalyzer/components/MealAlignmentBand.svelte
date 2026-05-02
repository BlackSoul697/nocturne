<script lang="ts">
  import { Rect } from 'layerchart/canvas';
  import { tryGetLaneContext, useThemeColors } from '../lib/lane-context.svelte';

  /**
   * Vertical translucent band that visualizes the active DIA window when
   * time-shift is on. Reads `alignMinute` and `dia` from {@link LaneContextState};
   * renders nothing when context is missing or either value is null. Must be
   * placed inside a `<Chart>` with a minute-of-day x-domain ([0, 1440]).
   */
  const ctx = tryGetLaneContext();
  const colors = useThemeColors(['--muted-foreground']);

  let endMinute = $derived(
    ctx?.alignMinute != null && ctx.dia != null ? ctx.alignMinute + ctx.dia * 60 : null,
  );
</script>

{#if ctx?.alignMinute != null && endMinute != null}
  <Rect
    x0={ctx.alignMinute}
    x1={Math.min(1440, endMinute)}
    y0={0}
    y1={Number.MAX_SAFE_INTEGER}
    fill={colors['--muted-foreground']}
    fillOpacity={0.08}
  />
{/if}
