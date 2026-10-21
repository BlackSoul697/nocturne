<!-- The days behind one picked slot, read from each day's coverage bitmaps, each linking to Day in Review. -->
<script lang="ts">
  import { resolve } from "$app/paths";
  import type { DailyProfileRange } from "$lib/api";
  import { Badge } from "$lib/components/ui/badge";
  import { Button } from "$lib/components/ui/button";
  import X from "@lucide/svelte/icons/x";
  import ArrowUpRight from "@lucide/svelte/icons/arrow-up-right";
  import { formatWeekdayDate, time } from "$lib/utils/formatting";
  import { startOfDay } from "$lib/utils/date-range";
  import {
    atMinute,
    clockTime,
    contributingDays,
    SLOT_MINUTES,
    type CoverageSeries,
  } from "./daily-profile-view";

  interface Props {
    slot: number;
    primary: DailyProfileRange | undefined;
    comparison?: DailyProfileRange | null;
    onclose: () => void;
  }

  let { slot, primary, comparison = null, onclose }: Props = $props();

  const minute = $derived(slot * SLOT_MINUTES);
  const groups = $derived(
    [
      { key: "primary", label: "This range", days: contributingDays(primary, slot) },
      ...(comparison
        ? [{ key: "comparison", label: "Comparison range", days: contributingDays(comparison, slot) }]
        : []),
    ]
  );

  const seriesLabels = $derived<Record<CoverageSeries, string>>({
    glucose: "Glucose",
    scheduledBasal: "Scheduled basal",
    deliveredBasal: "Delivered basal",
    netLoopCorrection: "Loop correction",
    iobCob: "IOB/COB",
  });
</script>

<section class="space-y-3 rounded-lg border bg-card p-4" aria-labelledby="slot-days-heading">
  <div class="flex items-start justify-between gap-2">
    <div>
      <h3 id="slot-days-heading" class="font-medium">
        Days behind {time(atMinute(minute))}–{time(atMinute(minute + SLOT_MINUTES))}
      </h3>
      <p class="text-sm text-muted-foreground">
        Each day whose data counted in this slot. Open one to see it in Day in Review at this time.
      </p>
    </div>
    <Button variant="ghost" size="icon-sm" onclick={onclose} aria-label="Close the day list">
      <X class="size-4" />
    </Button>
  </div>

  {#each groups as group (group.key)}
    <div class="space-y-1.5">
      {#if groups.length > 1}
        <h4 class="text-xs font-medium text-muted-foreground">{group.label}</h4>
      {/if}
      {#if group.days.length === 0}
        <p class="text-sm text-muted-foreground">No day contributed to this slot.</p>
      {:else}
        <ul class="max-h-96 divide-y overflow-y-auto rounded-md border">
          {#each group.days as entry (entry.day.date)}
            <li>
              <a
                href={resolve(`/reports/day-in-review?date=${entry.day.date}&time=${clockTime(minute)}`)}
                class="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-sm hover:bg-muted/50"
              >
                <span class="min-w-32 font-medium">{formatWeekdayDate(startOfDay(entry.day.date ?? ""))}</span>
                <span class="flex flex-1 flex-wrap gap-1">
                  {#each entry.series as series (series)}
                    <Badge variant="outline">{seriesLabels[series]}</Badge>
                  {/each}
                </span>
                <ArrowUpRight class="size-4 text-muted-foreground" />
              </a>
            </li>
          {/each}
        </ul>
      {/if}
    </div>
  {/each}
</section>
