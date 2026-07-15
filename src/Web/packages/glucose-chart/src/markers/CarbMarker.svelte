<script lang="ts">
  // Native SVG throughout: layerchart 2.x marks (Group/Text) each call
  // registerMark() on mount, and this renders once per carb entry, so a
  // component per treatment cost O(N^2) across the chart's mark deriveds.
  // Native <g>/<text> render identically while registering nothing.
  interface Props {
    xPos: number;
    yPos: number;
    carbs: number;
    label: string | null;
    treatmentId: string;
    onMarkerClick: (treatmentId: string) => void;
  }

  let { xPos, yPos, carbs, label, treatmentId, onMarkerClick }: Props =
    $props();
</script>

<!-- Mouse-only click, matching the original <Group onclick>; the chart is not a
     keyboard tab-stop surface (treatments are reachable via the data table). -->
<!-- svelte-ignore a11y_click_events_have_key_events -->
<g
  transform="translate({xPos}, {yPos})"
  role="button"
  aria-label="{carbs}g carbs"
  onclick={() => onMarkerClick(treatmentId)}
  class="cursor-pointer"
>
  <!-- Food/meal label above the marker -->
  {#if label}
    <text
      y={-18}
      text-anchor="middle"
      class="text-[7px] fill-carbs font-medium opacity-80"
    >
      {label}
    </text>
  {/if}
  <!-- Hemisphere (bowl shape - curves below baseline) -->
  <path
    d="M -8,0 A 8,8 0 0,0 8,0 Z"
    fill="var(--color-carbs)"
    class="opacity-90 hover:opacity-100 transition-opacity"
  />
  <text y={18} text-anchor="middle" class="text-[8px] fill-carbs font-medium">
    {carbs}g
  </text>
</g>
