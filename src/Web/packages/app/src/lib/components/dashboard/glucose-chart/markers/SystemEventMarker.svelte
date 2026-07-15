<script lang="ts">
  // Native <g> instead of layerchart's Group: Group calls registerMark() on
  // mount and this renders once per system event, so a component per event cost
  // O(N^2) across the chart's mark deriveds. <g> registers nothing.
  import { SystemEventIcon } from "$lib/components/icons";
  import { SystemEventType } from "$lib/api";

  interface Props {
    xPos: number;
    yPos: number;
    eventType?: SystemEventType;
    color: string;
  }

  let { xPos, yPos, eventType, color }: Props = $props();
</script>

<g transform="translate({xPos}, {yPos})">
  <!-- Icon using foreignObject to embed Lucide component -->
  <foreignObject x="-8" y="-8" width="16" height="16">
    <div class="flex items-center justify-center w-full h-full">
      <SystemEventIcon {eventType} size={16} {color} />
    </div>
  </foreignObject>
</g>
