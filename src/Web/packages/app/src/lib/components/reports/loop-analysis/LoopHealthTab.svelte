<!--
  The Loop health tab: how the loop ran, as the API measured it, in tiles for the range and strips
  for each hour of the day, with the gaps it found listed and linked to Day in Review. A figure the
  API could not measure reads as unknown, never as zero.
-->
<script lang="ts">
  import { resolve } from "$app/paths";
  import { Rule } from "layerchart";
  import {
    LoopAnalysisSeries,
    type LoopHealthGap,
    type LoopHealthGaps,
    type LoopHealthRange,
    type LoopHealthResponse,
    type LoopHealthShare,
  } from "$lib/api";
  import FigureStrip, { type Figure } from "$lib/components/reports/FigureStrip.svelte";
  import * as Alert from "$lib/components/ui/alert";
  import { Button } from "$lib/components/ui/button";
  import { plural } from "$lib/utils/plural";
  import { startOfDay } from "$lib/utils/date-range";
  import { formatMediumDate, formatWeekdayDate, hourLabel, time } from "$lib/utils/formatting";
  import ArrowUpRight from "@lucide/svelte/icons/arrow-up-right";
  import Clock from "@lucide/svelte/icons/clock";
  import EyeOff from "@lucide/svelte/icons/eye-off";
  import TimeOfDayPanel from "./TimeOfDayPanel.svelte";
  import HourBars from "./HourBars.svelte";
  import HourBands from "./HourBands.svelte";
  import StripKey, { type StripKeyItem } from "./StripKey.svelte";
  import NoTimeZone from "./NoTimeZone.svelte";
  import IgnoredFiltersNotice from "./IgnoredFiltersNotice.svelte";
  import AggregateReadout from "./AggregateReadout.svelte";
  import { TimeOfDayCursor } from "./time-of-day-cursor.svelte";
  import { atMinute, axisEnd } from "./daily-profile-view";
  import {
    beganIn,
    dayInReviewSearch,
    distributionHour,
    hourOfSlot,
    longestFirst,
    shareBars,
    shareHour,
    type HourBar,
  } from "./loop-health-view";

  let { response }: { response: LoopHealthResponse } = $props();

  const primary = $derived(response.primary);
  const comparison = $derived(response.comparison ?? null);
  const comparing = $derived(comparison != null);

  const withheld = $derived(new Set(response.withheldSeries ?? []));
  const hidden = (series: LoopAnalysisSeries) => withheld.has(series);
  const hiddenText = () => "Hidden: your access does not include this data";

  /** Analysed time leaves every activity share measured, even at zero; none measured means none analysed. */
  const analysed = (range: LoopHealthRange | null | undefined) =>
    [range?.activity?.closedLoop, range?.activity?.pump, range?.activity?.cgm, range?.zeroDelivery?.algorithmZeroTemp].some(
      (share) => share?.percent != null
    ) ||
    range?.automaticBoluses?.perDay?.overall != null ||
    range?.sensitivityRatio?.overall != null;
  const hasData = $derived(analysed(primary));
  const everythingHidden = $derived(
    [
      LoopAnalysisSeries.LoopActivity,
      LoopAnalysisSeries.CgmActivity,
      LoopAnalysisSeries.ZeroDelivery,
      LoopAnalysisSeries.AutomaticBoluses,
      LoopAnalysisSeries.SensitivityRatio,
    ].every(hidden)
  );

  /**
   * Whether the range held any loop cycles. Without cycles there are no gaps to count, so a gap
   * count of zero would claim a loop that never ran.
   */
  const cycled = (range: LoopHealthRange | null | undefined, gaps: LoopHealthGaps | null | undefined) =>
    gaps != null && (hidden(LoopAnalysisSeries.LoopActivity) || range?.activity?.closedLoop?.percent != null);

  const pct = (v: number) => `${v.toFixed(1)}%`;
  const maybePct = (v: number | null | undefined) => (v == null ? "–" : pct(v));
  const u1 = (v: number) => v.toFixed(1);
  const u2 = (v: number) => v.toFixed(2);
  const daysCounted = (n: number | null | undefined) => (n == null ? "–" : plural(n, ["# day", "# days"]));

  function duration(minutes: number | null | undefined): string {
    if (minutes == null) return "–";
    const m = Math.round(minutes);
    if (m < 60) return plural(m, ["# min", "# min"]);
    if (m >= 48 * 60) {
      const d = Math.floor(m / (24 * 60));
      const h = Math.round((m - d * 24 * 60) / 60);
      return h === 0 ? plural(d, ["# day", "# days"]) : `${plural(d, ["# day", "# days"])} ${plural(h, ["# h", "# h"])}`;
    }
    const h = Math.floor(m / 60);
    return m % 60 === 0 ? plural(h, ["# h", "# h"]) : `${plural(h, ["# h", "# h"])} ${plural(m % 60, ["# min", "# min"])}`;
  }

  /** A figure's note, with the comparison's value after it when there is one. */
  function noted(note: string | null, compared: string | null): string | undefined {
    const parts = [note, comparing ? `Compared: ${compared ?? "–"}` : null].filter((p): p is string => p != null);
    return parts.length > 0 ? parts.join(" · ") : undefined;
  }

  const hiddenFigure = (label: string): Figure => ({ label, value: "–", note: hiddenText() });
  const unknownFigure = (label: string, reason: string): Figure => ({ label, value: "Unknown", note: reason });

  function shareFigure(
    label: string,
    pick: (range: LoopHealthRange | null | undefined) => LoopHealthShare | null | undefined,
    note: string
  ): Figure {
    const value = pick(primary)?.percent;
    const compared = pick(comparison)?.percent;
    if (value == null) return { label, value: "–", note: noted("Nothing measured in this range", maybeComparedPct(compared)) };
    return { label, value: pct(value), note: noted(note, maybeComparedPct(compared)) };
  }
  const maybeComparedPct = (v: number | null | undefined) => (v == null ? null : pct(v));

  function gapsFigure(
    label: string,
    series: LoopAnalysisSeries,
    pick: (range: LoopHealthRange | null | undefined) => LoopHealthGaps | null | undefined,
    none: string
  ): Figure {
    if (hidden(series)) return hiddenFigure(label);
    const gaps = pick(primary);
    if (!cycled(primary, gaps)) return unknownFigure(label, "No loop cycles in this range");
    const count = gaps!.count ?? 0;
    const compared = pick(comparison);
    return {
      label,
      value: String(count),
      unit: plural(count, ["stretch", "stretches"]),
      note: noted(
        count > 0 ? `${duration(gaps!.totalMinutes)} in all, longest ${duration(gaps!.longestMinutes)}` : none,
        cycled(comparison, compared)
          ? `${plural(compared!.count ?? 0, ["# stretch", "# stretches"])}, ${duration(compared!.totalMinutes)} in all`
          : null
      ),
    };
  }

  const figures = $derived.by<Figure[]>(() => {
    const list: Figure[] = [];

    list.push(
      hidden(LoopAnalysisSeries.LoopActivity)
        ? hiddenFigure("Closed loop")
        : shareFigure("Closed loop", (r) => r?.activity?.closedLoop, "Of analysed time, with the loop in control")
    );
    list.push(
      hidden(LoopAnalysisSeries.CgmActivity)
        ? hiddenFigure("CGM active")
        : shareFigure("CGM active", (r) => r?.activity?.cgm, "Of analysed time, with glucose readings arriving")
    );
    list.push(
      hidden(LoopAnalysisSeries.LoopActivity)
        ? hiddenFigure("Pump active")
        : shareFigure("Pump active", (r) => r?.activity?.pump, "Of analysed time, with the pump reporting")
    );
    list.push(
      gapsFigure("Loop gaps", LoopAnalysisSeries.LoopGaps, (r) => r?.cadence?.loopGaps, "No stretch of more than 15 minutes between loop cycles")
    );
    list.push(
      gapsFigure(
        "Unrecorded delivery",
        LoopAnalysisSeries.UnrecordedDelivery,
        (r) => r?.cadence?.unrecordedDelivery,
        "The pump's records say what it delivered all the time the loop ran"
      )
    );

    if (hidden(LoopAnalysisSeries.ZeroDelivery)) list.push(hiddenFigure("Algorithm zero temps"));
    else {
      const zero = primary?.zeroDelivery;
      const value = zero?.algorithmZeroTemp?.percent;
      list.push({
        label: "Algorithm zero temps",
        value: maybePct(value),
        note: noted(
          value == null
            ? "No delivery is known in this range"
            : `Of time delivery is known, the loop set basal to zero. Suspended ${maybePct(zero?.suspended?.percent)}, manual zero temps ${maybePct(zero?.manualZeroTemp?.percent)}`,
          maybeComparedPct(comparison?.zeroDelivery?.algorithmZeroTemp?.percent)
        ),
      });
    }

    if (hidden(LoopAnalysisSeries.MaxBasal)) list.push(hiddenFigure("At max basal"));
    else {
      const maxBasal = primary?.maxBasal;
      const value = maxBasal?.atCap?.percent;
      const known = maxBasal?.capKnown?.percent;
      if (value == null) list.push(unknownFigure("At max basal", "No algorithm temp's cap is recorded in this range"));
      else
        list.push({
          label: "At max basal",
          value: pct(value),
          note: noted(
            `Of algorithm temp time whose cap is known: the cap is known when the loop recorded which limit applied, for ${maybePct(known)} of it`,
            maybeComparedPct(comparison?.maxBasal?.atCap?.percent)
          ),
        });
    }

    if (hidden(LoopAnalysisSeries.AtMaxIob)) list.push(hiddenFigure("At max IOB"));
    else if (primary?.atMaxIob?.percent == null)
      list.push(unknownFigure("At max IOB", "No loop cycle in this range says whether max IOB limited it"));
    else
      list.push(
        shareFigure(
          "At max IOB",
          (r) => r?.atMaxIob,
          "Of loop time with a recorded reason, the loop held back insulin it wanted because IOB was at its limit"
        )
      );

    if (hidden(LoopAnalysisSeries.AutomaticBoluses)) list.push(hiddenFigure("Automatic boluses a day"));
    else {
      const perDay = primary?.automaticBoluses?.perDay?.overall?.mean;
      const size = primary?.automaticBoluses?.size?.overall;
      const compared = comparison?.automaticBoluses?.perDay?.overall?.mean;
      list.push({
        label: "Automatic boluses a day",
        value: perDay == null ? "–" : u1(perDay),
        note: noted(
          perDay == null
            ? "Nothing measured in this range"
            : size?.p50 == null
              ? "No automatic boluses in this range"
              : `Median size ${u2(size.p50)} U` +
                (size.p10 != null && size.p90 != null ? `, P10–P90 ${u2(size.p10)}–${u2(size.p90)} U` : ""),
          compared == null ? null : u1(compared)
        ),
      });
    }

    if (hidden(LoopAnalysisSeries.SuggestedNotEnacted)) list.push(hiddenFigure("Suggested, not enacted"));
    else if (primary?.suggestedNotEnacted?.percent == null)
      list.push(unknownFigure("Suggested, not enacted", "The loop did not record its suggestions apart from what it enacted"));
    else
      list.push(
        shareFigure(
          "Suggested, not enacted",
          (r) => r?.suggestedNotEnacted,
          "Of the changes the loop asked for, those the pump did not carry out"
        )
      );

    if (hidden(LoopAnalysisSeries.LoopLimits)) list.push(hiddenFigure("Max basal rate setting"));
    else if (primary?.limits?.loopMaximumBasalRate == null)
      list.push(unknownFigure("Max basal rate setting", "No maximum basal rate setting is recorded"));
    else list.push({ label: "Max basal rate setting", value: u2(primary.limits.loopMaximumBasalRate), unit: "U/h" });

    if (hidden(LoopAnalysisSeries.OrefMaxIob)) list.push(hiddenFigure("Max IOB"));
    else if (primary?.limits?.orefMaxIob == null)
      list.push(unknownFigure("Max IOB", "No loop cycle in this range named its max IOB"));
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

  const percentEnd = (bars: HourBar[]) => Math.min(100, axisEnd(bars.map((b) => b.value), 5));
  const zeroDomain = $derived<[number, number]>([0, percentEnd([...zeroBars, ...zeroComparison])]);
  const capDomain = $derived<[number, number]>([0, percentEnd([...capBars, ...capComparison])]);
  const sensitivityDomain = $derived.by<[number, number]>(() => {
    const known = [...sensitivityHours, ...sensitivityComparison]
      .flatMap((h) => [h.value?.p25, h.value?.p75])
      .filter((v): v is number => v != null);
    const low = Math.min(1, ...known);
    const high = Math.max(1, ...known);
    const pad = Math.max(0.05, (high - low) * 0.1);
    return [Math.max(0, Math.floor((low - pad) * 10) / 10), Math.ceil((high + pad) * 10) / 10];
  });

  const CLOSED = "var(--chart-2)";
  const ZERO = "var(--chart-3)";
  const CAP = "var(--chart-4)";
  const SENSITIVITY = "var(--chart-2)";

  function barKey(color: string, label: string, bars: HourBar[]): StripKeyItem[] {
    return [
      ...(comparing ? [{ mark: "bar" as const, label, color }, { mark: "compared" as const, label: "Compared range" }] : []),
      ...(bars.some((b) => b.sparse) ? [{ mark: "sparse" as const, label: "Too few days", color }] : []),
    ];
  }

  const emptyText = (series: LoopAnalysisSeries, available: boolean, none: string) =>
    hidden(series) ? hiddenText() : available ? null : none;

  const hourText = (hour: number) => `${hourLabel(atMinute(hour * 60))}–${hourLabel(atMinute(hour * 60 + 60))}`;

  function describeShare(share: LoopHealthShare | null | undefined, slot: number): string {
    const hour = shareHour(share, hourOfSlot(slot));
    if (hour?.percent == null) return "Nothing measured in this hour";
    return hour.daily?.n == null ? pct(hour.percent) : `${pct(hour.percent)}, ${daysCounted(hour.daily.n)}`;
  }

  function describeSensitivity(slot: number): string {
    const value = distributionHour(primary?.sensitivityRatio, hourOfSlot(slot))?.value;
    if (value?.p50 == null) return "Nothing measured in this hour";
    return `Median ${u2(value.p50)}, ${plural(value.n ?? 0, ["# loop cycle", "# loop cycles"])}`;
  }

  const pickedHour = $derived(cursor.selected == null ? null : hourOfSlot(cursor.selected));
  const crossesYear = $derived(
    (response.primaryRange?.startDate ?? "").slice(0, 4) !== (response.primaryRange?.endDate ?? "").slice(0, 4)
  );
  const gapDate = (gap: LoopHealthGap) => {
    const day = startOfDay(gap.date ?? "");
    return crossesYear ? formatMediumDate(day) : formatWeekdayDate(day);
  };

  const LIST_LENGTH = 10;
  let expanded = $state<Record<string, boolean>>({});

  const gapLists = $derived(
    [
      {
        key: "loop-gaps",
        title: "Loop gaps",
        explanation: "More than 15 minutes between loop cycles.",
        series: LoopAnalysisSeries.LoopGaps,
        gaps: primary?.cadence?.loopGaps,
      },
      {
        key: "unrecorded-delivery",
        title: "Unrecorded delivery",
        explanation: "Time the loop ran while the pump's records do not say what it delivered.",
        series: LoopAnalysisSeries.UnrecordedDelivery,
        gaps: primary?.cadence?.unrecordedDelivery,
      },
    ].map((list) => {
      const all = beganIn(longestFirst(list.gaps), pickedHour);
      return { ...list, all, shown: expanded[list.key] ? all : all.slice(0, LIST_LENGTH) };
    })
  );
  const pickedCount = $derived(gapLists.filter((l) => !hidden(l.series)).reduce((sum, l) => sum + l.all.length, 0));
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
      <dd class="text-right">{maybePct(hour.daily?.p50)}</dd>
      <dt class="text-muted-foreground">Days counted</dt>
      <dd class="text-right">{daysCounted(hour.daily?.n)}</dd>
      {#if comparing}
        <dt class="text-muted-foreground">Compared, all days</dt>
        <dd class="text-right">{maybePct(other?.percent)}</dd>
      {/if}
    </dl>
    <p class="mt-1 text-muted-foreground">All days pools every day's time in this hour; median day is the middle day's share.</p>
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

    {#if !hasData}
      <div class="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
        {#if everythingHidden}
          <p class="font-medium">Your access does not include the data this tab is drawn from</p>
        {:else}
          <p class="font-medium">No data in this range</p>
          <p class="text-sm">Pick a longer range, or relax the filters.</p>
        {/if}
      </div>
    {:else}
      <FigureStrip {figures} />

      <div class="space-y-5 rounded-lg border bg-card p-3 @md:p-4">
        <div class="space-y-1">
          <TimeOfDayPanel
            id="closed-loop"
            title="Closed loop by hour"
            unit="Share of analysed time, %"
            {cursor}
            yDomain={[0, 100]}
            heightClass="h-28"
            describe={(slot) => describeShare(primary?.activity?.closedLoop, slot)}
            empty={emptyText(LoopAnalysisSeries.LoopActivity, closedBars.length > 0, "No loop activity in this range")}
          >
            {#snippet marks()}
              <HourBars bars={closedBars} comparison={closedComparison} color={CLOSED} />
            {/snippet}
            {#snippet tooltip(slot)}
              {@render shareReadout(primary?.activity?.closedLoop, comparison?.activity?.closedLoop, slot)}
            {/snippet}
          </TimeOfDayPanel>
          {#if !hidden(LoopAnalysisSeries.LoopActivity)}
            <StripKey items={barKey(CLOSED, "Closed loop", closedBars)} />
          {/if}
        </div>

        <div class="space-y-1">
          <TimeOfDayPanel
            id="zero-delivery"
            title="Algorithm zero temps by hour"
            unit="Share of time the pump's records say what it delivered, %"
            {cursor}
            yDomain={zeroDomain}
            heightClass="h-28"
            describe={(slot) => describeShare(primary?.zeroDelivery?.algorithmZeroTemp, slot)}
            empty={emptyText(LoopAnalysisSeries.ZeroDelivery, zeroBars.length > 0, "No delivery is known in this range")}
          >
            {#snippet marks()}
              <HourBars bars={zeroBars} comparison={zeroComparison} color={ZERO} />
            {/snippet}
            {#snippet tooltip(slot)}
              {@const hour = hourOfSlot(slot)}
              {@const suspended = shareHour(primary?.zeroDelivery?.suspended, hour)?.percent}
              {@const manual = shareHour(primary?.zeroDelivery?.manualZeroTemp, hour)?.percent}
              {@render shareReadout(primary?.zeroDelivery?.algorithmZeroTemp, comparison?.zeroDelivery?.algorithmZeroTemp, slot)}
              <p class="mt-1 tabular-nums">
                <span class="text-muted-foreground">Suspended</span> {maybePct(suspended)}
                <span class="ml-2 text-muted-foreground">Manual zero temps</span> {maybePct(manual)}
              </p>
            {/snippet}
          </TimeOfDayPanel>
          {#if !hidden(LoopAnalysisSeries.ZeroDelivery)}
            <StripKey items={barKey(ZERO, "Algorithm zero temps", zeroBars)} />
          {/if}
        </div>

        <div class="space-y-1">
          <TimeOfDayPanel
            id="max-basal"
            title="At max basal by hour"
            unit="Share of algorithm temp time whose cap is known, %"
            {cursor}
            yDomain={capDomain}
            heightClass="h-28"
            describe={(slot) => describeShare(primary?.maxBasal?.atCap, slot)}
            empty={emptyText(LoopAnalysisSeries.MaxBasal, capBars.length > 0, "Unknown: no algorithm temp's cap is recorded in this range")}
          >
            {#snippet marks()}
              <HourBars bars={capBars} comparison={capComparison} color={CAP} />
            {/snippet}
            {#snippet tooltip(slot)}
              {@const known = shareHour(primary?.maxBasal?.capKnown, hourOfSlot(slot))?.percent}
              {@render shareReadout(primary?.maxBasal?.atCap, comparison?.maxBasal?.atCap, slot)}
              <p class="mt-1 tabular-nums">
                <span class="text-muted-foreground">Cap known for</span> {maybePct(known)}
              </p>
            {/snippet}
          </TimeOfDayPanel>
          {#if !hidden(LoopAnalysisSeries.MaxBasal)}
            <StripKey items={barKey(CAP, "At max basal", capBars)} />
          {/if}
        </div>

        <div class="space-y-1">
          <TimeOfDayPanel
            id="sensitivity"
            title="Sensitivity ratio by hour"
            unit="Autosens or dynamic ISF; above 1 means more insulin needed"
            {cursor}
            yDomain={sensitivityDomain}
            yFormat={(v) => v.toFixed(1)}
            heightClass="h-32"
            showTimeAxis
            describe={describeSensitivity}
            empty={emptyText(
              LoopAnalysisSeries.SensitivityRatio,
              sensitivityHours.some((h) => h.value?.p50 != null),
              "No sensitivity ratio reported in this range"
            )}
          >
            {#snippet marks()}
              <Rule y={1} class="stroke-foreground/40" />
              <HourBands hours={sensitivityHours} comparison={sensitivityComparison} color={SENSITIVITY} />
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
              <p class="mt-1 text-muted-foreground">n counts loop cycles, each reporting one ratio.</p>
            {/snippet}
          </TimeOfDayPanel>
          {#if !hidden(LoopAnalysisSeries.SensitivityRatio)}
            <StripKey
              items={[
                { mark: "band", label: "25–75%", color: SENSITIVITY },
                { mark: "line", label: "Median", color: SENSITIVITY },
                { mark: "reference", label: "1.0 = usual sensitivity" },
                ...(comparing ? [{ mark: "compared" as const, label: "Compared range" }] : []),
                ...(sensitivityHours.some((h) => h.value != null && (h.value.sparse ?? true))
                  ? [{ mark: "sparse" as const, label: "Too few days", color: SENSITIVITY }]
                  : []),
              ]}
            />
          {/if}
        </div>

        <p class="flex items-center gap-1.5 px-1 text-xs text-muted-foreground">
          <EyeOff class="size-3.5" />
          Pale hours rest on too few days to read as a pattern. Click an hour, or press Enter on it,
          to list only the gaps that began in it. With the keyboard, the arrow keys move along the
          day and between charts.
        </p>
      </div>

      <section class="space-y-4 rounded-lg border bg-card p-4" aria-labelledby="loop-health-gaps">
        <div class="flex flex-wrap items-baseline justify-between gap-2">
          <h3 id="loop-health-gaps" class="font-medium">Gaps, longest first</h3>
          {#if pickedHour != null}
            <Button variant="link" size="inline" onclick={() => (cursor.selected = null)}>Show every hour</Button>
          {/if}
        </div>
        {#if pickedHour != null}
          <p class="text-sm text-muted-foreground">Only those that began {hourText(pickedHour)}.</p>
        {/if}
        <p class="sr-only" aria-live="polite">
          {#if pickedHour != null}{plural(pickedCount, ["# gap began in this hour", "# gaps began in this hour"])}{/if}
        </p>

        {#each gapLists as list (list.key)}
          <div class="space-y-1.5">
            <h4 class="text-sm font-medium">{list.title}</h4>
            {#if hidden(list.series)}
              <p class="text-sm text-muted-foreground">{hiddenText()}</p>
            {:else if !cycled(primary, list.gaps)}
              <p class="text-sm text-muted-foreground">Unknown: no loop cycles in this range</p>
            {:else}
              <p class="text-sm text-muted-foreground">{list.explanation}</p>
              {#if list.all.length === 0}
                <p class="text-sm text-muted-foreground">None</p>
              {:else}
                <ul id={`${list.key}-list`} class="max-h-96 divide-y overflow-y-auto rounded-md border">
                  {#each list.shown as gap, i (`${gap.start}${i}`)}
                    <li>
                      <a
                        href={resolve(`/reports/day-in-review?${dayInReviewSearch(gap)}`)}
                        class="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-sm hover:bg-muted/50"
                      >
                        <span class="min-w-32 font-medium">{gapDate(gap)}</span><span class="sr-only">, </span>
                        <span class="min-w-16 tabular-nums">{time(atMinute(gap.startMinuteOfDay ?? 0))}</span><span class="sr-only">, </span>
                        <span class="flex-1 tabular-nums">{duration(gap.minutes)}</span>
                        <span class="sr-only">. Open in Day in Review</span>
                        <ArrowUpRight class="size-4 text-muted-foreground" aria-hidden="true" />
                      </a>
                    </li>
                  {/each}
                </ul>
                {#if list.all.length > LIST_LENGTH}
                  <Button
                    variant="link"
                    size="inline"
                    aria-expanded={expanded[list.key] ?? false}
                    aria-controls={`${list.key}-list`}
                    onclick={() => (expanded = { ...expanded, [list.key]: !expanded[list.key] })}
                  >
                    {expanded[list.key]
                      ? plural(LIST_LENGTH, ["Show the longest #", "Show the longest #"])
                      : plural(list.all.length, ["Show all #", "Show all #"])}
                  </Button>
                {/if}
              {/if}
            {/if}
          </div>
        {/each}
      </section>
    {/if}
  </div>
{/if}
