<script lang="ts">
  import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Badge } from "$lib/components/ui/badge";
  import { Button } from "$lib/components/ui/button";
  import { Switch } from "$lib/components/ui/switch";
  import { RailItemId } from "$lib/api/generated/nocturne-api-client";
  import { RAIL_ITEM_IDS } from "$lib/components/status-pills/rail";
  import ArrowDown from "@lucide/svelte/icons/arrow-down";
  import ArrowUp from "@lucide/svelte/icons/arrow-up";
  import GripVertical from "@lucide/svelte/icons/grip-vertical";
  import Pill from "@lucide/svelte/icons/pill";
  import RotateCcw from "@lucide/svelte/icons/rotate-ccw";
  import { DragReorder, moveItem } from "./drag-reorder.svelte";

  interface Props {
    /** The rail the dashboard shows now, in order. */
    value: readonly RailItemId[];
    /** The rail a reset goes back to. */
    defaultItems: readonly RailItemId[];
    onchange?: (items: RailItemId[]) => void;
  }

  let { value, defaultItems, onchange }: Props = $props();

  const LABELS: Record<RailItemId, string> = {
    [RailItemId.Cob]: "Carbs on board (COB)",
    [RailItemId.Basal]: "Basal rate",
    [RailItemId.Iob]: "Insulin on board (IOB)",
    [RailItemId.Loop]: "Loop status",
    [RailItemId.Reservoir]: "Reservoir",
    [RailItemId.Trackers]: "Trackers",
  };

  const shown = $derived(value.filter((id) => RAIL_ITEM_IDS.includes(id)));
  const hidden = $derived(RAIL_ITEM_IDS.filter((id) => !shown.includes(id)));
  const isDefault = $derived(
    shown.length === defaultItems.length && shown.every((id, i) => id === defaultItems[i])
  );

  const drag = new DragReorder<RailItemId>(
    () => shown,
    (items) => onchange?.(items)
  );

  function setShown(id: RailItemId, show: boolean) {
    onchange?.(show ? [...shown, id] : shown.filter((item) => item !== id));
  }

  function move(index: number, to: number) {
    onchange?.(moveItem(shown, index, to));
  }
</script>

<Card>
  <CardHeader>
    <CardTitle class="flex items-center gap-2">
      <Pill class="h-5 w-5" />
      Status Pills
    </CardTitle>
    <CardDescription>
      Select the pills that show next to the glucose value. Drag a pill to change its position.
    </CardDescription>
  </CardHeader>
  <CardContent class="space-y-4">
    <div class="space-y-2" role="list" aria-label="Shown pills">
      {#each shown as id, index (id)}
        <div
          class="flex items-center gap-2 rounded-lg border bg-card p-3 transition-all
          {drag.dragOverIndex === index ? 'border-primary bg-accent' : 'border-border'}
          {drag.draggedIndex === index ? 'opacity-50' : ''}"
          draggable="true"
          ondragstart={(e) => drag.start(e, index)}
          ondragover={(e) => drag.over(e, index)}
          ondragleave={() => drag.leave()}
          ondrop={(e) => drag.drop(e, index)}
          ondragend={() => drag.end()}
          role="listitem"
          data-rail-item={id}
        >
          <GripVertical class="h-4 w-4 cursor-grab text-muted-foreground" />
          <Badge variant="outline" class="h-6 min-w-6 tabular-nums">
            {index + 1}
          </Badge>
          <span class="flex-1 min-w-0 text-sm font-medium">{LABELS[id]}</span>
          <Button
            variant="ghost-muted"
            size="icon-sm"
            aria-label={`Move ${LABELS[id]} up`}
            disabled={index === 0}
            onclick={() => move(index, index - 1)}
          >
            <ArrowUp class="h-4 w-4" />
          </Button>
          <Button
            variant="ghost-muted"
            size="icon-sm"
            aria-label={`Move ${LABELS[id]} down`}
            disabled={index === shown.length - 1}
            onclick={() => move(index, index + 1)}
          >
            <ArrowDown class="h-4 w-4" />
          </Button>
          <Switch
            checked={true}
            aria-label={`Show ${LABELS[id]}`}
            onCheckedChange={(checked: boolean) => setShown(id, checked)}
          />
        </div>
      {/each}

      {#if shown.length === 0}
        <p class="rounded-lg border border-dashed py-6 text-center text-sm text-muted-foreground">
          No pills show on the dashboard.
        </p>
      {/if}
    </div>

    {#if hidden.length > 0}
      <div class="space-y-2" role="list" aria-label="Hidden pills">
        <span class="text-sm font-medium text-muted-foreground">Hidden</span>
        {#each hidden as id (id)}
          <div
            class="flex items-center gap-2 rounded-lg border border-dashed p-3"
            role="listitem"
            data-rail-item={id}
          >
            <span class="flex-1 min-w-0 text-sm text-muted-foreground">{LABELS[id]}</span>
            <Switch
              checked={false}
              aria-label={`Show ${LABELS[id]}`}
              onCheckedChange={(checked: boolean) => setShown(id, checked)}
            />
          </div>
        {/each}
      </div>
    {/if}

    <div class="flex items-center justify-between gap-2">
      <p class="text-xs text-muted-foreground">
        Trackers show only when a tracker is active.
      </p>
      <Button
        variant="outline"
        size="sm"
        disabled={isDefault}
        onclick={() => onchange?.([...defaultItems])}
      >
        <RotateCcw class="h-4 w-4" />
        Reset to default
      </Button>
    </div>
  </CardContent>
</Card>
