<!--
  The Loop health tab: how the loop ran, as the API measured it, in tiles for the range and strips
  for each hour of the day, with the gaps it found listed and linked to Day in Review. A figure the
  API could not measure reads as unknown, with why, never as zero.
-->
<script lang="ts">
  import { resolve } from "$app/paths";
  import { Rule } from "layerchart";
  import {
    LoopAnalysisSeries,
    type LoopHealthDistribution,
    type LoopHealthGaps,
    type LoopHealthResponse,
    type LoopHealthShare,
  } from "$lib/api";
  import FigureStrip, { type Figure } from "$lib/components/reports/FigureStrip.svelte";
  import * as Alert from "$lib/components/ui/alert";
  import { Badge } from "$lib/components/ui/badge";
  import { Button } from "$lib/components/ui/button";
  import { plural } from "$lib/utils/plural";
  import { startOfDay } from "$lib/utils/date-range";
  import { formatWeekdayDate, hourLabel, time } from "$lib/utils/formatting";
  import ArrowUpRight from "@lucide/svelte/icons/arrow-up-right";
  import Clock from "@lucide/svelte/icons/clock";
  import EyeOff from "@lucide/svelte/icons/eye-off";
  import TimeOfDayPanel from "./TimeOfDayPanel.svelte";
  import SlotBars from "./SlotBars.svelte";
  import HourBands from "./HourBands.svelte";
  import NoTimeZone from "./NoTimeZone.svelte";
  import IgnoredFiltersNotice from "./IgnoredFiltersNotice.svelte";
  import AggregateReadout from "./AggregateReadout.svelte";
  import { TimeOfDayCursor } from "./time-of-day-cursor.svelte";
  import { atMinute, axisEnd } from "./daily-profile-view";
  import {
    dayInReviewSearch,
    distributionHour,
    gapRows,
    hourOfSlot,
    shareBars,
    shareHour,
    type GapRow,
  } from "./loop-health-view";

  let { response }: { response: LoopHealthResponse } = $props();

  const primary = $derived(response.primary);
  const comparison = $derived(response.comparison ?? null);
  const comparing = $derived(comparison != null);
  const hasDays = $derived((primary?.days?.length ?? 0) > 0);

  const withheld = $derived(new Set(response.withheldSeries ?? []));
  const hidden = (series: LoopAnalysisSeries) => withheld.has(series);
  const hiddenText = () => "Hidden: your access does not include this data";

  const pct = (v: number) => `${v.toFixed(1)}%`;
  const u1 = (v: number) => v.toFixed(1);
  const u2 = (v: number) => v.toFixed(2);
  const daysText = (n: number | undefined) => plural(n ?? 0, ["# day", "# days"]);

  function duration(minutes: number | null | undefined): string {
    const m = Math.round(minutes ?? 0);
    if (m < 60) return plural(m, ["# min", "# min"]);
    const h = Math.floor(m / 60);
    return m % 60 === 0 ? `${h} h` : `${h} h ${m % 60} min`;
  }

  /** A figure's comparison, when there is one, after its own note. */
  function noted(note: string | null, compared: string | null): string | undefined {
    const parts = [note, comparing ? `Compared: ${compared ?? "–"}` : null].filter((p): p is string => p != null);
    return parts.length > 0 ? parts.join(" · ") : undefined;
  }

  const hiddenFigure = (label: string): Figure => ({ label, value: "–", note: hiddenText() });
  const unknownFigure = (label: string, reason: string): Figure => ({ label, value: "Unknown", note: reason });

  /** A share as a tile: its pooled percentage for the range, or none measured. */
  function shareFigure(
    label: string,
    series: LoopAnalysisSeries,
    pick: (range: typeof primary) => LoopHealthShare | null | undefined,
    note: string | null = null
  ): Figure {
    if (hidden(series)) return hiddenFigure(label);
    const value = pick(primary)?.percent;
    const compared = pick(comparison ?? undefined)?.percent;
    if (value == null)
      return { label, value: "–", note: noted("Nothing measured in this range", compared == null ? null : pct(compared)) };
    return { label, value: pct(value), note: noted(note, compared == null ? null : pct(compared)) };
  }

  function gapsFigure(label: string, series: LoopAnalysisSeries, pick: (range: typeof primary) => LoopHealthGaps | null | undefined, note: string): Figure {
    if (hidden(series)) return hiddenFigure(label);
    const gaps = pick(primary);
    const compared = pick(comparison ?? undefined);
    const count = gaps?.count ?? 0;
    return {
      label,
      value: String(count),
      unit: plural(count, ["stretch", "stretches"]),
      note: noted(
        count > 0 ? `${duration(gaps?.totalMinutes)} in all, longest ${duration(gaps?.longestMinutes)}` : note,
        compared ? `${compared.count ?? 0}, ${duration(compared.totalMinutes)} in all` : null
      ),
    };
  }

  const figures = $derived.by<Figure[]>(() => {
    const activity = primary?.activity;
    const list: Figure[] = [
      shareFigure(
        "Closed loop",
        LoopAnalysisSeries.LoopActivity,
        (r) => r?.activity?.closedLoop,
        activity?.aidActivePercent != null ? `AID active ${pct(activity.aidActivePercent)} over the whole range, unfiltered` : null
      ),
      shareFigure(
        "CGM active",
        LoopAnalysisSeries.CgmActivity,
        (r) => r?.activity?.cgm,
        activity?.cgmActivePercent != null ? `${pct(activity.cgmActivePercent)} over the whole range, unfiltered` : null
      ),
      shareFigure(
        "Pump active",
        LoopAnalysisSeries.LoopActivity,
        (r) => r?.activity?.pump,
        activity?.pumpUsePercent != null ? `${pct(activity.pumpUsePercent)} over the whole range, unfiltered` : null
      ),
      gapsFigure("Loop gaps", LoopAnalysisSeries.LoopGaps, (r) => r?.cadence?.loopGaps, "No stretch of more than 15 minutes between loop cycles"),
      gapsFigure(
        "Unrecorded delivery",
        LoopAnalysisSeries.UnrecordedDelivery,
        (r) => r?.cadence?.unrecordedDelivery,
        "The pump's records cover all the time the loop ran"
      ),
    ];

    if (hidden(LoopAnalysisSeries.ZeroDelivery)) list.push(hiddenFigure("Algorithm zero temps"));
    else {
      const zero = primary?.zeroDelivery;
      const value = zero?.algorithmZeroTemp?.percent;
      const compared = comparison?.zeroDelivery?.algorithmZeroTemp?.percent;
      list.push({
        label: "Algorithm zero temps",
        value: value == null ? "–" : pct(value),
        note: noted(
          value == null
            ? "Delivery is not known in this range"
            : `Suspended ${pct(zero?.suspended?.percent ?? 0)}, manual zero temps ${pct(zero?.manualZeroTemp?.percent ?? 0)}`,
          compared == null ? null : pct(compared)
        ),
      });
    }

    if (hidden(LoopAnalysisSeries.MaxBasal)) list.push(hiddenFigure("At max basal"));
    else {
      const maxBasal = primary?.maxBasal;
      const value = maxBasal?.atCap?.percent;
      const compared = comparison?.maxBasal?.atCap?.percent;
      if (value == null)
        list.push(
          unknownFigure(
            "At max basal",
            maxBasal?.capKnown?.percent == null
              ? "No algorithm temp basals in this range"
              : "The loop did not record which limit was in force for any temp"
          )
        );
      else
        list.push({
          label: "At max basal",
          value: pct(value),
          note: noted(`Cap known for ${pct(maxBasal?.capKnown?.percent ?? 0)} of algorithm temp time`, compared == null ? null : pct(compared)),
        });
    }

    if (hidden(LoopAnalysisSeries.AtMaxIob)) list.push(hiddenFigure("At max IOB"));
    else if (primary?.atMaxIob?.percent == null)
      list.push(unknownFigure("At max IOB", "Only oref systems (AAPS, Trio, OpenAPS) say when max IOB limits them"));
    else list.push(shareFigure("At max IOB", LoopAnalysisSeries.AtMaxIob, (r) => r?.atMaxIob));

    if (hidden(LoopAnalysisSeries.AutomaticBoluses)) list.push(hiddenFigure("Automatic boluses"));
    else {
      const boluses = primary?.automaticBoluses;
      const perDay = boluses?.perDay?.overall;
      const size = boluses?.size?.overall;
      const compared = comparison?.automaticBoluses?.perDay?.overall?.mean;
      list.push({
        label: "Automatic boluses",
        value: perDay?.mean == null ? "–" : u1(perDay.mean),
        unit: perDay?.mean == null ? undefined : "per day",
        note: noted(
          size?.p50 == null
            ? "No automatic boluses in this range"
            : `Median ${u2(size.p50)} U, P10–P90 ${u2(size.p10 ?? 0)}–${u2(size.p90 ?? 0)} U`,
          compared == null ? null : `${u1(compared)} per day`
        ),
      });
    }

    if (hidden(LoopAnalysisSeries.SuggestedNotEnacted)) list.push(hiddenFigure("Suggested, not enacted"));
    else if (primary?.suggestedNotEnacted?.percent == null)
      list.push(
        unknownFigure("Suggested, not enacted", "Only oref systems record what they suggested apart from what they enacted")
      );
    else list.push(shareFigure("Suggested, not enacted", LoopAnalysisSeries.SuggestedNotEnacted, (r) => r?.suggestedNotEnacted));

    if (hidden(LoopAnalysisSeries.LoopLimits)) list.push(hiddenFigure("Loop max basal rate"));
    else if (primary?.limits?.loopMaximumBasalRate == null)
      list.push(unknownFigure("Loop max basal rate", "Only Loop records its maximum basal rate setting"));
    else list.push({ label: "Loop max basal rate", value: u2(primary.limits.loopMaximumBasalRate), unit: "U/h" });

    if (hidden(LoopAnalysisSeries.OrefMaxIob)) list.push(hiddenFigure("Max IOB"));
    else if (primary?.limits?.orefMaxIob == null)
      list.push(unknownFigure("Max IOB", "Recorded only when an oref system says it limited a dose"));
    else list.push({ label: "Max IOB", value: u1(primary.limits.orefMaxIob), unit: "U" });

    return list;
  });

  const cursor = new TimeOfDayCursor();

  const closedBars = $derived(shareBars(primary?.activity?.closedLoop));
  const closedComparison = $derived(shareBars(comparison?.activity?.closedLoop));
  const zeroBars = $derived(shareBars(primary?.zeroDelivery?.algorithmZeroTemp));
  const zeroComparison = $derived(shareBars(comparison?.zeroDelivery?.algorithmZeroTemp));
  const capBars = $derived(shareBars(primary?.maxBasal?.atCap));
  const capComparison = $derived(shareBars(comparison?.maxBasal?.atCap));
  const sensitivityHours = $derived(primary?.sensitivityRatio?.hours ?? []);
  const sensitivityComparison = $derived(comparison?.sensitivityRatio?.hours ?? []);

  const percentEnd = (bars: { value: number }[]) => Math.min(100, axisEnd(bars.map((b) => b.value), 5));
  const zeroDomain = $derived<[number, number]>([0, percentEnd([...zeroBars, ...zeroComparison])]);
  const capDomain = $derived<[number, number]>([0, percentEnd([...capBars, ...capComparison])]);
  const sensitivityDomain = $derived.by<[number, number]>(() => {
    const values = [...sensitivityHours, ...sensitivityComparison].flatMap((h) => [h.value?.p25, h.value?.p75]);
    const known = values.filter((v): v is number => v != null);
    if (known.length === 0) return [0.5, 1.5];
    return [Math.floor(Math.min(0.9, ...known) * 10) / 10, Math.ceil(Math.max(1.1, ...known) * 10) / 10];
  });

  const emptyText = (series: LoopAnalysisSeries, available: boolean, none: string) =>
    hidden(series) ? hiddenText() : available ? null : none;

  const hourText = (hour: number) => `${hourLabel(atMinute(hour * 60))}–${hourLabel(atMinute(hour * 60 + 60))}`;

  function describeShare(share: LoopHealthShare | null | undefined, slot: number): string {
    const hour = shareHour(share, hourOfSlot(slot));
    if (hour?.percent == null) return "Nothing measured in this hour";
    return `${pct(hour.percent)}, ${daysText(hour.daily?.n)}`;
  }

  function describeDistribution(distribution: LoopHealthDistribution | null | undefined, slot: number): string {
    const value = distributionHour(distribution, hourOfSlot(slot))?.value;
    if (!value) return "Nothing measured in this hour";
    return `Median ${u2(value.p50 ?? 0)}, ${daysText(value.n)}`;
  }

  // Picking an hour on a strip narrows the gap list to the gaps that began in it.
  const pickedHour = $derived(cursor.selected == null ? null : hourOfSlot(cursor.selected));
  const allGaps = $derived(gapRows(primary?.cadence?.loopGaps, primary?.cadence?.unrecordedDelivery));
  const gapsInHour = $derived(
    pickedHour == null ? allGaps : allGaps.filter((row) => Math.floor((row.gap.startMinuteOfDay ?? 0) / 60) === pickedHour)
  );
  const LIST_LENGTH = 10;
  let showAllGaps = $state(false);
  const listedGaps = $derived(showAllGaps ? gapsInHour : gapsInHour.slice(0, LIST_LENGTH));
  const gapsHidden = $derived(hidden(LoopAnalysisSeries.LoopGaps) && hidden(LoopAnalysisSeries.UnrecordedDelivery));
  const kindLabel = (row: GapRow) => (row.kind === "loop" ? "Loop gap" : "Unrecorded delivery");
</script>

{#snippet shareReadout(share: LoopHealthShare | null | undefined, compared: LoopHealthShare | null | undefined, slot: number)}
  {@const hour = shareHour(share, hourOfSlot(slot))}
  {@const other = shareHour(compared, hourOfSlot(slot))}
  <p class="text-muted-foreground">{hourText(hourOfSlot(slot))}</p>
  {#if hour?.percent == null}
    <p class="text-muted-foreground">Nothing measured in this hour</p>
  {:else}
    <dl class="grid grid-cols-[auto_auto] gap-x-3 tabular-nums">
      <dt class="text-muted-foreground">All days</dt>
      <dd class="text-right">{pct(hour.percent)}</dd>
      <dt class="text-muted-foreground">Median day</dt>
      <dd class="text-right">{hour.daily?.p50 == null ? "–" : pct(hour.daily.p50)}</dd>
      <dt class="text-muted-foreground">n</dt>
      <dd class="text-right">{daysText(hour.daily?.n)}</dd>
      {#if comparing}
        <dt class="text-muted-foreground">Compared</dt>
        <dd class="text-right">{other?.percent == null ? "–" : pct(other.percent)}</dd>
      {/if}
    </dl>
    {#if hour.daily?.sparse ?? true}
      <p class="text-muted-foreground">Too few days to read as a pattern</p>
    {/if}
  {/if}
{/snippet}

{#if response.timeZoneUnavailableReason}
  <NoTimeZone reason={response.timeZoneUnavailableReason} />
{:else}
  <div class="space-y-6">
    {#if response.historyClamped}
      <Alert.Root variant="info">
        <Clock />
        <Alert.Title>Only the last 24 hours are included</Alert.Title>
        <Alert.Description>
          You can read only the most recent 24 hours of this data, so the report covers those 24
          hours whatever range is picked, compares nothing, and each hour of the day rests on at
          most one day.
        </Alert.Description>
      </Alert.Root>
    {/if}

    <IgnoredFiltersNotice ignored={response.ignoredFilters} />

    <FigureStrip {figures} />

    {#if !hasDays}
      <div class="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
        <p class="font-medium">No data in this range</p>
        <p class="text-sm">Pick a longer range, or relax the filters.</p>
      </div>
    {:else}
      <div class="space-y-5 rounded-lg border bg-card p-3 @md:p-4">
        {#if comparing}
          <p class="text-xs text-muted-foreground">The comparison range is drawn as dashed outlines over each hour.</p>
        {/if}

        <TimeOfDayPanel
          id="closed-loop"
          title="Closed loop by hour"
          unit="% of analysed time"
          {cursor}
          yDomain={[0, 100]}
          heightClass="h-28"
          describe={(slot) => describeShare(primary?.activity?.closedLoop, slot)}
          empty={emptyText(LoopAnalysisSeries.LoopActivity, closedBars.length > 0, "No loop activity in this range")}
        >
          {#snippet marks()}
            <SlotBars bars={closedBars} comparison={closedComparison} color="var(--chart-1)" />
          {/snippet}
          {#snippet tooltip(slot)}
            {@render shareReadout(primary?.activity?.closedLoop, comparison?.activity?.closedLoop, slot)}
          {/snippet}
        </TimeOfDayPanel>

        <TimeOfDayPanel
          id="zero-delivery"
          title="Algorithm zero temps by hour"
          unit="% of time delivery is known"
          {cursor}
          yDomain={zeroDomain}
          heightClass="h-28"
          describe={(slot) => describeShare(primary?.zeroDelivery?.algorithmZeroTemp, slot)}
          empty={emptyText(LoopAnalysisSeries.ZeroDelivery, zeroBars.length > 0, "Delivery is not known in this range")}
        >
          {#snippet marks()}
            <SlotBars bars={zeroBars} comparison={zeroComparison} color="var(--insulin-basal)" />
          {/snippet}
          {#snippet tooltip(slot)}
            {@const hour = hourOfSlot(slot)}
            {@const suspended = shareHour(primary?.zeroDelivery?.suspended, hour)?.percent}
            {@const manual = shareHour(primary?.zeroDelivery?.manualZeroTemp, hour)?.percent}
            {@render shareReadout(primary?.zeroDelivery?.algorithmZeroTemp, comparison?.zeroDelivery?.algorithmZeroTemp, slot)}
            <p class="mt-1 tabular-nums">
              <span class="text-muted-foreground">Suspended</span> {suspended == null ? "–" : pct(suspended)}
              <span class="ml-2 text-muted-foreground">Manual zero temps</span> {manual == null ? "–" : pct(manual)}
            </p>
          {/snippet}
        </TimeOfDayPanel>

        <TimeOfDayPanel
          id="max-basal"
          title="At max basal by hour"
          unit="% of algorithm temp time whose cap is known"
          {cursor}
          yDomain={capDomain}
          heightClass="h-28"
          describe={(slot) => describeShare(primary?.maxBasal?.atCap, slot)}
          empty={emptyText(
            LoopAnalysisSeries.MaxBasal,
            capBars.length > 0,
            "Unknown: no algorithm temp's cap is known in this range"
          )}
        >
          {#snippet marks()}
            <SlotBars bars={capBars} comparison={capComparison} color="var(--chart-4)" />
          {/snippet}
          {#snippet tooltip(slot)}
            {@const known = shareHour(primary?.maxBasal?.capKnown, hourOfSlot(slot))?.percent}
            {@render shareReadout(primary?.maxBasal?.atCap, comparison?.maxBasal?.atCap, slot)}
            <p class="mt-1 tabular-nums">
              <span class="text-muted-foreground">Cap known for</span> {known == null ? "–" : pct(known)}
            </p>
          {/snippet}
        </TimeOfDayPanel>

        <TimeOfDayPanel
          id="sensitivity"
          title="Sensitivity ratio by hour"
          unit="autosens or dynamic ISF"
          {cursor}
          yDomain={sensitivityDomain}
          yFormat={(v) => v.toFixed(1)}
          heightClass="h-32"
          showTimeAxis
          describe={(slot) => describeDistribution(primary?.sensitivityRatio, slot)}
          empty={emptyText(
            LoopAnalysisSeries.SensitivityRatio,
            sensitivityHours.some((h) => h.value != null),
            "No sensitivity ratio reported in this range"
          )}
          legend={[
            { texture: "percentile-inner", label: "25–75%", color: "color-mix(in oklab, var(--chart-2) 30%, transparent)" },
            { texture: "percentile-median", label: "Median", color: "var(--chart-2)", shape: "line" },
          ]}
        >
          {#snippet marks()}
            <Rule y={1} class="stroke-foreground/40" />
            <HourBands hours={sensitivityHours} comparison={sensitivityComparison} color="var(--chart-2)" />
          {/snippet}
          {#snippet tooltip(slot)}
            {@const hour = hourOfSlot(slot)}
            <p class="text-muted-foreground">{hourText(hour)}</p>
            <AggregateReadout
              value={distributionHour(primary?.sensitivityRatio, hour)?.value}
              comparison={distributionHour(comparison?.sensitivityRatio, hour)?.value}
              showComparison={comparing}
              format={u2}
              counts="readings"
            />
          {/snippet}
        </TimeOfDayPanel>

        <p class="flex items-center gap-1.5 px-1 text-xs text-muted-foreground">
          <EyeOff class="size-3.5" />
          Faded hours rest on too few days to read as a pattern. Click an hour, or press Enter on it,
          to list only the gaps that began in it. With the keyboard, the arrow keys move along the
          day and between charts.
        </p>
      </div>

      <section class="space-y-3 rounded-lg border bg-card p-4" aria-labelledby="loop-health-gaps">
        <div class="flex flex-wrap items-baseline justify-between gap-2">
          <h3 id="loop-health-gaps" class="font-medium">
            {#if pickedHour == null}Gaps, longest first{:else}Gaps that began {hourText(pickedHour)}{/if}
          </h3>
          {#if pickedHour != null}
            <Button variant="link" size="inline" onclick={() => (cursor.selected = null)}>Show every hour</Button>
          {/if}
        </div>
        <p class="text-sm text-muted-foreground">
          A loop gap is more than 15 minutes between loop cycles; unrecorded delivery is time the
          loop ran while the pump's records do not say what it delivered. Open one to see it in Day
          in Review.
        </p>
        {#if gapsHidden}
          <p class="text-sm text-muted-foreground">{hiddenText()}</p>
        {:else if listedGaps.length === 0}
          <p class="text-sm text-muted-foreground">No gaps{pickedHour == null ? " in this range" : " began in this hour"}.</p>
        {:else}
          <ul class="max-h-96 divide-y overflow-y-auto rounded-md border">
            {#each listedGaps as row, i (`${row.kind}${row.gap.start}${i}`)}
              <li>
                <a
                  href={resolve(`/reports/day-in-review?${dayInReviewSearch(row.gap)}`)}
                  class="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-sm hover:bg-muted/50"
                >
                  <span class="min-w-32 font-medium">{formatWeekdayDate(startOfDay(row.gap.date ?? ""))}</span>
                  <span class="min-w-16 tabular-nums">{time(atMinute(row.gap.startMinuteOfDay ?? 0))}</span>
                  <span class="min-w-24 tabular-nums">{duration(row.gap.minutes)}</span>
                  <span class="flex-1"><Badge variant="outline">{kindLabel(row)}</Badge></span>
                  <ArrowUpRight class="size-4 text-muted-foreground" />
                </a>
              </li>
            {/each}
          </ul>
          {#if gapsInHour.length > LIST_LENGTH}
            <Button variant="link" size="inline" onclick={() => (showAllGaps = !showAllGaps)}>
              {showAllGaps ? "Show the longest 10" : `Show all ${gapsInHour.length}`}
            </Button>
          {/if}
        {/if}
      </section>
    {/if}
  </div>
{/if}
