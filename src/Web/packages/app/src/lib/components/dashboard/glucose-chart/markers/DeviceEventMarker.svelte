<script lang="ts">
  // Native <g> instead of layerchart's Group: Group calls registerMark() on
  // mount and this renders once per device event, so a component per event cost
  // O(N^2) across the chart's mark deriveds. <g> registers nothing.
  import { DeviceEventIcon } from "$lib/components/icons";
  import type { DeviceEventType } from "$lib/api";

  interface Props {
    xPos: number;
    yPos: number;
    eventType?: DeviceEventType;
    color: string;
    treatmentId?: string;
    onMarkerClick?: (treatmentId: string) => void;
  }

  let { xPos, yPos, eventType, color, treatmentId, onMarkerClick }: Props =
    $props();

  const clickable = $derived(Boolean(treatmentId && onMarkerClick));
</script>

<!-- Mouse-only click, matching the original <Group onclick>; the chart is not a
     keyboard tab-stop surface (events are reachable via the data table). -->
<!-- svelte-ignore a11y_click_events_have_key_events -->
<g
  transform="translate({xPos}, {yPos})"
  role={clickable ? "button" : undefined}
  aria-label={clickable ? `${eventType ?? "device"} event` : undefined}
  onclick={clickable ? () => onMarkerClick?.(treatmentId ?? "") : undefined}
  class={clickable ? "cursor-pointer" : ""}
>
  <!-- Background circle -->
  <circle
    r="12"
    fill="var(--background)"
    stroke={color}
    stroke-width="2"
    class="opacity-95 {treatmentId && onMarkerClick
      ? 'hover:opacity-100 transition-opacity'
      : ''}"
  />
  <!-- Icon using foreignObject to embed Lucide component -->
  <foreignObject x="-10" y="-10" width="20" height="20">
    <div class="flex items-center justify-center w-full h-full">
      <DeviceEventIcon {eventType} size={16} {color} />
    </div>
  </foreignObject>
</g>
