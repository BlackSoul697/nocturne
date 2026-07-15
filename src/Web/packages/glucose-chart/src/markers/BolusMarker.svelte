<script lang="ts">
  // Native SVG throughout: layerchart 2.x marks (Group/Text/Polygon) each call
  // registerMark() on mount, and this renders once per bolus, so a component per
  // treatment cost O(N^2) across the chart's mark deriveds. Native <g>/<text>/
  // <polygon> render identically while registering nothing.
  interface Props {
    xPos: number;
    yPos: number;
    insulin: number;
    isOverride: boolean;
    treatmentId: string;
    onMarkerClick: (treatmentId: string) => void;
  }

  let { xPos, yPos, insulin, isOverride, treatmentId, onMarkerClick }: Props =
    $props();
</script>

<!-- Mouse-only click, matching the original <Group onclick>; the chart is not a
     keyboard tab-stop surface (treatments are reachable via the data table). -->
<!-- svelte-ignore a11y_click_events_have_key_events -->
<g
  transform="translate({xPos}, {yPos})"
  role="button"
  aria-label="{insulin.toFixed(1)}U bolus"
  onclick={() => onMarkerClick(treatmentId)}
  class="cursor-pointer"
>
  {#if isOverride}
    <!-- Triangle for manual override -->
    <polygon
      points="0,12 -8,0 8,0"
      class="opacity-90 fill-insulin-bolus hover:opacity-100 transition-opacity"
    />
  {:else}
    <!-- Hemisphere (dome shape - curves above baseline) -->
    <path
      d="M -8,0 A 8,8 0 0,1 8,0 Z"
      class="opacity-90 fill-insulin-bolus hover:opacity-100 transition-opacity"
    />
  {/if}
  <text
    y={-14}
    text-anchor="middle"
    class="text-[8px] fill-insulin-bolus font-medium"
  >
    {insulin.toFixed(1)}U
  </text>
</g>
