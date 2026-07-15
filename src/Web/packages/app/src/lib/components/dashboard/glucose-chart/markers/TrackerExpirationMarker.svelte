<script lang="ts">
  // Native SVG throughout: layerchart 2.x marks (Group/Rect/Text) each call
  // registerMark() on mount, and this renders once per tracker expiration, so a
  // component per marker cost O(N^2) across the chart's mark deriveds. Native
  // <g>/<rect>/<text> render identically while registering nothing.
  import { TrackerCategoryIcon } from "$lib/components/icons";
  import type { TrackerCategory } from "$lib/api";

  interface Props {
    xPos: number;
    lineTop: number;
    lineBottom: number;
    basalTrackTop: number;
    time: Date;
    category?: TrackerCategory;
    color: string;
  }

  let { xPos, lineTop, lineBottom, basalTrackTop, time, category, color }: Props =
    $props();
</script>

<!-- Dashed vertical line spanning the chart height -->
<line
  x1={xPos}
  y1={lineTop}
  x2={xPos}
  y2={lineBottom}
  stroke={color}
  stroke-width="1.5"
  stroke-dasharray="4,4"
  class="opacity-60"
/>
<!-- Icon and label at the top of the basal track -->
<g transform="translate({xPos}, {basalTrackTop + 10})">
  <!-- Background pill -->
  <rect
    x={-24}
    y={-8}
    width={48}
    height={16}
    rx="8"
    fill="var(--background)"
    stroke={color}
    stroke-width="1"
    class="opacity-90"
  />
  <!-- Icon using foreignObject -->
  <foreignObject x="-22" y="-6" width="12" height="12">
    <div class="flex items-center justify-center w-full h-full">
      <TrackerCategoryIcon {category} size={10} {color} />
    </div>
  </foreignObject>
  <!-- Time label -->
  <text
    x={3}
    y={0}
    text-anchor="start"
    class="text-[7px] fill-muted-foreground font-medium"
    dy="0.35em"
  >
    {time.toLocaleTimeString([], {
      hour: "numeric",
      minute: "2-digit",
    })}
  </text>
</g>
