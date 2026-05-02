<script lang="ts">
  import { invalidateAll } from '$app/navigation';
  import { contextResource } from '$lib/hooks/resource-context.svelte';
  import { requireDateParamsContext } from '$lib/hooks/date-params.svelte';
  import { getData } from '$lib/api/generated/loopalyzers.generated.remote';
  import { aggregateLane, type LaneAggregate } from '../lib/aggregate';
  import { applyShift, type ShiftConfig } from '../lib/time-shift';
  import {
    createLaneContext,
    BIN_COUNT,
  } from '../lib/lane-context.svelte';
  import {
    useLoopalyzerParams,
    decodeTimeWindow,
    decodeEventTypes,
    encodeTimeWindow,
    encodeEventTypes,
  } from '../lib/url-state.svelte';
  import LoopalyzerHeader from './LoopalyzerHeader.svelte';
  import LoopalyzerProfilesTable from './LoopalyzerProfilesTable.svelte';
  import BgLane from './BgLane.svelte';
  import ScheduledBasalLane from './ScheduledBasalLane.svelte';
  import TempBasalLane from './TempBasalLane.svelte';
  import IobLane from './IobLane.svelte';
  import CobLane from './CobLane.svelte';
  import type {
    LoopalyzerDay,
    LoopalyzerResponse,
  } from '$lib/api/generated/nocturne-api-client';

  // Date range comes from the shared reports filter (14-day default).
  const dateParams = requireDateParamsContext(14);
  const params = useLoopalyzerParams();
  const laneCtx = createLaneContext();

  // Reactively re-fetch when the date range changes.
  const dataResource = contextResource(
    () => getData({ from: dateParams.from ?? undefined, to: dateParams.to ?? undefined }),
    { errorTitle: 'Error loading Loopalyzer' },
  );

  type ViewMode = 'average' | 'spaghetti';

  let data = $derived<LoopalyzerResponse | null>(dataResource.current ?? null);
  let rawDays = $derived<ReadonlyArray<LoopalyzerDay>>(data?.days ?? []);
  let isMultiDay = $derived(rawDays.length > 1);
  let viewMode = $derived<ViewMode>(params.viewMode === 'spaghetti' ? 'spaghetti' : 'average');
  let timeShift = $derived(params.timeShift === 1);
  let tsWindow = $derived(decodeTimeWindow(params.tsWindow));

  let shiftConfig = $derived<ShiftConfig>({
    window: tsWindow,
    minCarbs: params.tsMinCarbs ?? 10,
    eventTypes: decodeEventTypes(params.tsEventTypes),
  });

  let shifted = $derived(
    timeShift && isMultiDay
      ? applyShift(rawDays, shiftConfig)
      : { days: rawDays, avgMealMinute: null, shiftBins: rawDays.map(() => 0) },
  );
  let workingDays = $derived(shifted.days);

  // Patient's actual calendar today (in their tz). Spaghetti highlight uses this
  // only when it actually appears in the selected range.
  let actualTodayDate = $derived.by(() => {
    const tz = data?.timezone;
    if (!tz) return null;
    try {
      // 'en-CA' yields YYYY-MM-DD.
      return new Intl.DateTimeFormat('en-CA', { timeZone: tz }).format(new Date());
    } catch {
      return null;
    }
  });
  let todayDate = $derived(
    actualTodayDate != null && rawDays.some((d) => d.date === actualTodayDate)
      ? actualTodayDate
      : null,
  );

  // Multi-day "schedule" semantics: take the day whose ISO date string is largest.
  // We pull from rawDays — schedule is profile-bound and unaffected by meal time-shift.
  let mostRecentDay = $derived.by<LoopalyzerDay | null>(() => {
    if (rawDays.length === 0) return null;
    let best = rawDays[0];
    for (let i = 1; i < rawDays.length; i++) {
      if ((rawDays[i].date ?? '') > (best.date ?? '')) best = rawDays[i];
    }
    return best;
  });

  function lanePerDay<T>(getter: (d: LoopalyzerDay) => ReadonlyArray<T | null> | undefined): { date: string; bins: (T | null)[] }[] {
    return workingDays.map((d) => ({
      date: d.date ?? '',
      bins: ensureLength(getter(d) ?? [], BIN_COUNT),
    }));
  }

  function ensureLength<T>(arr: ReadonlyArray<T | null>, n: number): (T | null)[] {
    if (arr.length === n) return [...arr];
    const out: (T | null)[] = new Array(n).fill(null);
    for (let i = 0; i < Math.min(arr.length, n); i++) out[i] = arr[i] ?? null;
    return out;
  }

  let bgDays = $derived(lanePerDay<number>((d) => d.sgv));
  let iobDays = $derived(lanePerDay<number>((d) => d.iob));
  let cobDays = $derived(lanePerDay<number>((d) => d.cob));

  let bgAggregate = $derived<LaneAggregate>(aggregateLane(bgDays.map((d) => d.bins)));
  let iobAggregate = $derived<LaneAggregate>(aggregateLane(iobDays.map((d) => d.bins)));
  let cobAggregate = $derived<LaneAggregate>(aggregateLane(cobDays.map((d) => d.bins)));

  // Scheduled basal in multi-day uses most recent day's schedule (Grill 9c).
  let scheduledBasal = $derived.by<number[]>(() => {
    const src = mostRecentDay?.scheduledBasal;
    if (!src) return new Array(BIN_COUNT).fill(0);
    if (src.length === BIN_COUNT) return [...src];
    const out = new Array(BIN_COUNT).fill(0);
    for (let i = 0; i < Math.min(src.length, BIN_COUNT); i++) out[i] = src[i] ?? 0;
    return out;
  });
  let tempBasal = $derived.by<(number | null)[]>(() =>
    ensureLength(mostRecentDay?.tempBasal ?? [], BIN_COUNT),
  );

  // Single-day overlays (only meaningful when range = 1).
  let singleDay = $derived(rawDays.length === 1 ? rawDays[0] : null);
  let predictions = $derived(params.predictions === 1 ? (singleDay?.predictions ?? []) : []);
  let meals = $derived(singleDay?.meals ?? []);
  let boluses = $derived(singleDay?.boluses ?? []);
  let siteChanges = $derived(singleDay?.siteChanges ?? []);
  let sensorChanges = $derived(singleDay?.sensorChanges ?? []);

  /**
   * The report adds value only when at least one day in the range has APS data.
   * Without it the lanes still render, but the IOB/COB curves degrade to the
   * pure-treatments fallback and predictions/APS bands disappear — surface a
   * clearer empty state instead of silently showing a flat report.
   */
  let hasAnyApsData = $derived(rawDays.some((d) => d.hasApsData));

  // Drive the meal-alignment band via lane context.
  $effect(() => {
    laneCtx.alignMinute = timeShift && isMultiDay ? shifted.avgMealMinute : null;
    laneCtx.dia = data?.mostRecentDia ?? null;
  });

  function setViewMode(m: 'average' | 'spaghetti') {
    params.viewMode = m;
  }
  function setTimeShift(v: boolean) {
    params.timeShift = v ? 1 : 0;
  }
  function setTimeShiftSettings(s: { startMin: number; endMin: number; minCarbs: number }) {
    params.tsWindow = encodeTimeWindow({ startMin: s.startMin, endMin: s.endMin });
    params.tsMinCarbs = s.minCarbs;
  }
  function setShowPredictions(v: boolean) {
    params.predictions = v ? 1 : 0;
  }
  function setShowApsBands(v: boolean) {
    params.apsBands = v ? 1 : 0;
  }
  function setShowProfilesTable(v: boolean) {
    params.profilesTable = v ? 1 : 0;
  }
  function refresh() {
    invalidateAll();
  }

  function formatMin(min: number): string {
    const h = Math.floor(min / 60);
    const m = min % 60;
    return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
  }
</script>

{#if dataResource.current && rawDays.length > 0 && !hasAnyApsData}
  <div class="p-4">
    <div class="rounded-md border border-dashed bg-card p-8 text-center text-muted-foreground">
      <p class="text-base font-medium">Not enough data</p>
      <p class="mt-1 text-sm">
        Loopalyzer requires APS data (Loop / Trio / AAPS / iAPS). No closed-loop
        snapshots were found in the selected range.
      </p>
    </div>
  </div>
{:else if dataResource.current}
  <div class="space-y-4 p-4">
    <LoopalyzerHeader
      rangeDays={rawDays.length}
      {isMultiDay}
      timezone={data?.timezone ?? 'UTC'}
      {viewMode}
      onViewModeChange={setViewMode}
      timeShift={timeShift}
      onTimeShiftChange={setTimeShift}
      tsWindowStart={formatMin(tsWindow.startMin)}
      tsWindowEnd={formatMin(tsWindow.endMin)}
      tsMinCarbs={params.tsMinCarbs ?? 10}
      onTimeShiftSettingsChange={setTimeShiftSettings}
      showPredictions={params.predictions === 1}
      onShowPredictionsChange={setShowPredictions}
      showApsBands={params.apsBands === 1}
      onShowApsBandsChange={setShowApsBands}
      showProfilesTable={params.profilesTable === 1}
      onShowProfilesTableChange={setShowProfilesTable}
      onRefresh={refresh}
    />

    <div class="rounded-md border bg-card">
      <BgLane
        aggregate={bgAggregate}
        days={bgDays.map((d) => ({ date: d.date, sgv: d.bins }))}
        {viewMode}
        {todayDate}
        bgLow={data?.mostRecentBgLow ?? null}
        bgHigh={data?.mostRecentBgHigh ?? null}
        predictions={predictions}
        meals={meals}
        boluses={boluses}
        siteChanges={siteChanges}
        sensorChanges={sensorChanges}
      />
      <ScheduledBasalLane bins={scheduledBasal} />
      <TempBasalLane tempBins={tempBasal} scheduledBins={scheduledBasal} />
      <IobLane
        aggregate={iobAggregate}
        days={iobDays}
        {viewMode}
        {todayDate}
      />
      <CobLane
        aggregate={cobAggregate}
        days={cobDays}
        {viewMode}
        {todayDate}
        showXAxis
      />
    </div>

    {#if params.profilesTable === 1}
      <LoopalyzerProfilesTable profiles={data?.profiles ?? []} />
    {/if}
  </div>
{/if}
