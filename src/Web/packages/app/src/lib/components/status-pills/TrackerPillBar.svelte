<script lang="ts">
  import TrackerPill from "./TrackerPill.svelte";
  import LegacyAgePill from "./LegacyAgePill.svelte";
  import type {
    LegacyConsumableKind,
    LegacyFallbackDto,
    TrackerInstanceDto,
    TrackerDefinitionDto,
  } from "$lib/api";
  import { TrackerCategory } from "$lib/api";
  import { cn } from "$lib/utils";
  import { trackerDefinition, visibleTrackerInstances } from "./tracker-pill-visibility";

  interface TrackerPillBarProps {
    /** Active tracker instances */
    instances: TrackerInstanceDto[];
    /** Tracker definitions for metadata */
    definitions: TrackerDefinitionDto[];
    /** The legacy ages to show after the trackers, already narrowed to the visible ones. */
    fallbacks?: LegacyFallbackDto[];
    /** Epoch milliseconds the reached level is judged against. */
    now: number;
    /** Additional CSS classes */
    class?: string;
    /** Callback when complete button is clicked */
    onComplete?: (
      instanceId: string,
      instanceName: string,
      category: TrackerCategory,
      definitionId: string,
      completionEventType?: string
    ) => void;
    /** Opens the conversion of the legacy ages with one kind selected. */
    onCreateTracker?: (kind: LegacyConsumableKind) => void;
  }

  let {
    instances = [],
    definitions = [],
    fallbacks = [],
    now,
    class: className,
    onComplete,
    onCreateTracker,
  }: TrackerPillBarProps = $props();

  const visibleInstances = $derived(visibleTrackerInstances(instances, definitions, now));

  const hasVisiblePills = $derived(visibleInstances.length > 0 || fallbacks.length > 0);
</script>

{#if hasVisiblePills}
  <div
    class={cn("flex flex-wrap items-center gap-x-2 gap-y-1", className)}
    data-testid="tracker-pill-bar"
  >
    {#each visibleInstances as instance (instance.id)}
      <TrackerPill
        {instance}
        definition={trackerDefinition(instance, definitions)}
        {now}
        {onComplete}
      />
    {/each}
    {#each fallbacks as fallback (fallback.kind)}
      <LegacyAgePill {fallback} {now} {onCreateTracker} />
    {/each}
  </div>
{/if}
