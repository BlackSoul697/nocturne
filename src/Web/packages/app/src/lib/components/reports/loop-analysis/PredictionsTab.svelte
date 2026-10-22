<!--
  The Predictions tab: how far each curve the loop predicted with landed from the readings that
  followed, by horizon, by hour of day and by context. Every figure is the API's; the tab picks the
  axis, the curve shown in the detail, and lays the panels out.
-->
<script lang="ts">
  import {
    LoopAnalysisSeries,
    PredictionContext,
    PredictionCurve,
    type PredictionAccuracyResponse,
    type PredictionCurveAccuracy,
  } from "$lib/api";
  import * as Alert from "$lib/components/ui/alert";
  import * as ToggleGroup from "$lib/components/ui/toggle-group";
  import FigureStrip, { type Figure } from "$lib/components/reports/FigureStrip.svelte";
  import ChartKey, { type ChartKeyItem } from "$lib/components/charts/print/ChartKey.svelte";
  import { aidAlgorithmLabels } from "$lib/components/patient/labels";
  import { glucoseUnits } from "$lib/stores/appearance-store.svelte";
  import { bgDelta, bgLabel, convertToDisplayUnits } from "$lib/utils/formatting";
  import { plural } from "$lib/utils/plural";
  import Clock from "@lucide/svelte/icons/clock";
  import EyeOff from "@lucide/svelte/icons/eye-off";
  import Info from "@lucide/svelte/icons/info";
  import Lock from "@lucide/svelte/icons/lock";
  import HorizonErrorChart from "./HorizonErrorChart.svelte";
  import PredictionBiasHeatmap from "./PredictionBiasHeatmap.svelte";
  import PredictionContextTable from "./PredictionContextTable.svelte";
  import IgnoredFiltersNotice from "./IgnoredFiltersNotice.svelte";
  import NoTimeZone from "./NoTimeZone.svelte";
  import { axisEnd } from "./daily-profile-view";
  import { curveLabel, horizonList } from "./prediction-labels.svelte";

  let { response }: { response: PredictionAccuracyResponse } = $props();

  const primary = $derived(response.primary);
  const comparison = $derived(response.comparison ?? null);
  const curves = $derived(primary?.curves ?? []);
  const comparing = $derived(comparison != null);

  const withheld = $derived(new Set(response.withheldSeries ?? []));
  const curvesHidden = $derived(withheld.has(LoopAnalysisSeries.PredictionCurves));
  const devicesHidden = $derived(withheld.has(LoopAnalysisSeries.PredictionCount));
  const contextsHidden = $derived(withheld.has(LoopAnalysisSeries.PredictionContexts));

  const units = $derived(glucoseUnits.current);
  const unit = $derived(bgLabel());
  const toDisplay = $derived((mgdl: number) => convertToDisplayUnits(mgdl, units));
  const signed = (mgdl: number) => bgDelta(mgdl);
  const unsigned = (mgdl: number) => bgDelta(mgdl, false);

  const CURVE_ORDER = [
    PredictionCurve.Default,
    PredictionCurve.Iob,
    PredictionCurve.ZeroTemp,
    PredictionCurve.Cob,
    PredictionCurve.Uam,
  ];
  const CURVE_COLORS: Record<PredictionCurve, string> = {
    [PredictionCurve.Default]: "var(--pred-main)",
    [PredictionCurve.Iob]: "var(--pred-iob)",
    [PredictionCurve.ZeroTemp]: "var(--pred-zt)",
    [PredictionCurve.Cob]: "var(--pred-cob)",
    [PredictionCurve.Uam]: "var(--pred-uam)",
  };

  const curveOf = (range: typeof primary | null, kind: PredictionCurve) =>
    range?.curves?.find((c) => c.curve === kind) ?? null;

  /** Every curve either range carried, so one only the comparison has still shows. */
  const kinds = $derived(
    CURVE_ORDER.filter((kind) => curveOf(primary, kind) != null || curveOf(comparison, kind) != null)
  );

  /** One symmetric axis for every curve, so their spreads compare at a glance. */
  const yDomain = $derived.by<[number, number]>(() => {
    const ends = [...curves, ...(comparison?.curves ?? [])].flatMap((c) =>
      (c.horizons ?? []).flatMap((h) => [h.error?.p10, h.error?.p90].map((v) => (v == null ? null : Math.abs(toDisplay(v)))))
    );
    const end = axisEnd(ends, units === "mmol" ? 1 : 20);
    return [-end, end];
  });

  let picked = $state<PredictionCurve | null>(null);
  const detail = $derived(curves.find((c) => c.curve === picked) ?? curves[0] ?? null);

  const has = (kind: PredictionCurve) => kinds.includes(kind);
  const horizonsOf = (c: PredictionCurveAccuracy) => c.horizons ?? [];
  const settingsSignal = $derived(
    horizonList((curves[0]?.horizons ?? []).filter((h) => h.settingsSignal).map((h) => h.minutes ?? 0))
  );
  const anySparse = $derived(curves.some((c) => horizonsOf(c).some((h) => h.error?.sparse)));
  const anyInterrupted = $derived(curves.some((c) => horizonsOf(c).some((h) => (h.interrupted ?? 0) > 0)));

  const hiddenCurves = $derived(primary?.hiddenCurves ?? []);

  const key = $derived.by<ChartKeyItem[]>(() => [
    { texture: "percentile-outer", label: "10–90%", color: "color-mix(in oklab, var(--muted-foreground) 28%, transparent)" },
    { texture: "percentile-inner", label: "25–75%", color: "color-mix(in oklab, var(--muted-foreground) 55%, transparent)" },
    { texture: "percentile-median", label: "Bias", color: "var(--foreground)", shape: "line" },
    ...(settingsSignal ? [{ texture: "in-range" as const, label: "Settings signal", color: "var(--muted)" }] : []),
  ]);

  const figures = $derived.by<Figure[]>(() => {
    const list: Figure[] = [
      {
        label: "Predictions analysed",
        value: (primary?.predictions ?? 0).toLocaleString(),
        note: comparing
          ? `${(comparison?.predictions ?? 0).toLocaleString()} in the compared range`
          : "One per device per five minutes",
      },
    ];
    if ((primary?.algorithms?.length ?? 0) > 0)
      list.push({ label: "From", value: (primary?.algorithms ?? []).map((a) => aidAlgorithmLabels[a]).join(", ") });
    return list;
  });
</script>

{#if response.timeZoneUnavailableReason}
  <NoTimeZone reason={response.timeZoneUnavailableReason} />
{:else}
  <div class="space-y-6">
    {#if response.historyClamped}
      <Alert.Root variant="info">
        <Clock />
        <Alert.Title>Only the last 24 hours are included</Alert.Title>
        <Alert.Description>
          You can read only the most recent 24 hours of this data, so these figures cover the
          predictions made in those 24 hours whatever range is picked, and compare nothing. The most
          recent predictions are left out until their readings arrive.
        </Alert.Description>
      </Alert.Root>
    {/if}

    <IgnoredFiltersNotice ignored={response.ignoredFilters} />

    {#if curvesHidden}
      <div class="flex flex-col items-center gap-3 rounded-lg border border-dashed px-6 py-10 text-center">
        <Lock class="size-10 text-muted-foreground/50" />
        <h2 class="font-medium">Prediction accuracy is hidden</h2>
        <p class="max-w-prose text-sm text-muted-foreground">
          {#if devicesHidden}
            The predictions come from your loop's device status, and your access does not include
            device data.
          {:else}
            The predictions are compared with your glucose readings, and your access does not
            include glucose.
          {/if}
          Someone who manages this account can grant it.
        </p>
      </div>
    {:else}
      <FigureStrip {figures} />

      {#if hiddenCurves.length > 0}
        <p class="flex items-start gap-1.5 px-1 text-sm text-muted-foreground">
          <EyeOff class="mt-0.5 size-4 shrink-0" />
          {plural(hiddenCurves.length, ["This curve is hidden", "These curves are hidden"])}:
          {hiddenCurves.map(curveLabel).join(", ")}. Each is read only in the contexts the algorithm
          relies on it in, and those come from treatments, which your access does not include.
        </p>
      {/if}

      {#if kinds.length === 0}
        <div class="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
          <p class="font-medium">No predictions in this range</p>
          <p class="text-sm">
            Prediction accuracy needs the predictions your loop uploads with its device status.
            {#if response.historyClamped}
              Your access covers only the last 24 hours, which hold none yet.
            {:else}
              Pick a longer range, or relax the filters.
            {/if}
          </p>
        </div>
      {:else}
        <Alert.Root variant="info">
          <Info />
          <Alert.Title>Reading these figures</Alert.Title>
          <Alert.Description>
            <ul class="list-disc space-y-1 pl-4">
              <li>
                Bias is the prediction minus the reading that followed: positive where the
                prediction ran high. MAE, the mean absolute error, is the average size of the miss,
                whichever way it went.
              </li>
              {#if settingsSignal}
                <li>
                  Only the bias at {settingsSignal} is a sign of your settings. The readings at longer
                  horizons include everything the loop did after the prediction, which the
                  prediction assumed would not happen.
                </li>
              {/if}
              <li>
                Predictions followed by a carb entry or manual bolus before their horizon are
                counted as interrupted and left out of every bias figure, since the reading then
                measures that action.
              </li>
              {#if has(PredictionCurve.Uam)}
                <li>
                  oref emits its UAM curve only while it sees glucose rising faster than insulin
                  alone predicts, so UAM accuracy is measured only in that time.
                </li>
              {/if}
              {#if has(PredictionCurve.ZeroTemp)}
                <li>
                  oref cuts its zero-temp curve where it turns up above target, so its long
                  horizons come only from predictions that stayed low.
                </li>
              {/if}
            </ul>
          </Alert.Description>
        </Alert.Root>

        <section class="space-y-3 rounded-lg border bg-card p-3 @md:p-4" aria-labelledby="prediction-error-title">
          <div class="space-y-1 px-1">
            <h2 id="prediction-error-title" class="font-medium">Error by horizon</h2>
            <p class="text-xs text-muted-foreground">
              The spread of prediction minus reading, in {unit}, at each horizon: the bias as the
              bold line, with the MAE and the number of predictions above.
            </p>
          </div>
          <div class="flex flex-wrap items-center gap-x-4 gap-y-1 px-1">
            <ChartKey class="justify-start" items={key} />
            {#if comparing}
              <span class="flex items-center gap-1.5 text-xs text-muted-foreground">
                <svg viewBox="0 0 44 12" class="inline-block h-3 w-11 shrink-0" aria-hidden="true">
                  <line x1="0" y1="6" x2="44" y2="6" class="stroke-foreground" stroke-width="1.5" stroke-dasharray="3 2" />
                </svg>
                Compared range
              </span>
            {/if}
          </div>
          <div class="grid gap-x-6 gap-y-8 @2xl:grid-cols-2">
            {#each kinds as kind (kind)}
              <HorizonErrorChart
                {kind}
                curve={curveOf(primary, kind)}
                comparison={curveOf(comparison, kind)}
                color={CURVE_COLORS[kind]}
                {yDomain}
                scale={toDisplay}
                formatError={signed}
                formatMae={unsigned}
                {unit}
              />
            {/each}
          </div>
          {#if anySparse}
            <p class="flex items-center gap-1.5 px-1 text-xs text-muted-foreground">
              <EyeOff class="size-3.5" />
              Faded horizons rest on too few days to read as a pattern.
            </p>
          {/if}
          <p class="px-1 text-xs text-muted-foreground">
            With the keyboard, the arrow keys move between horizons, and Home and End go to either end.
          </p>
        </section>

        {#if detail}
          <section class="space-y-4 rounded-lg border bg-card p-3 @md:p-4" aria-labelledby="prediction-detail-title">
            <div class="flex flex-wrap items-center justify-between gap-2 px-1">
              <h2 id="prediction-detail-title" class="font-medium">By hour and context</h2>
              {#if curves.length > 1}
                <ToggleGroup.Root
                  type="single"
                  variant="segmented"
                  size="xs"
                  aria-label="Curve"
                  value={detail.curve ?? ""}
                  onValueChange={(next: string) => {
                    const curve = curves.find((c) => c.curve === next)?.curve;
                    if (curve) picked = curve;
                  }}
                >
                  {#each curves as curve (curve.curve)}
                    <ToggleGroup.Item value={curve.curve ?? ""}>{curveLabel(curve.curve)}</ToggleGroup.Item>
                  {/each}
                </ToggleGroup.Root>
              {/if}
            </div>

            <div class="space-y-2">
              <h3 class="px-1 text-sm font-medium">{curveLabel(detail.curve)} bias by hour of day</h3>
              <p class="px-1 text-xs text-muted-foreground">
                By the local hour the prediction was made in, in {unit}.
                {#if comparing}The compared range is in the charts above.{/if}
              </p>
              <PredictionBiasHeatmap curve={detail} formatError={signed} {unit} />
            </div>

            <div class="space-y-2">
              <h3 class="px-1 text-sm font-medium">{curveLabel(detail.curve)} by context</h3>
              {#if contextsHidden}
                <p class="px-1 text-sm text-muted-foreground">
                  Hidden: the contexts and the interrupted counts are drawn from carbs, boluses,
                  overrides and exercise, and your access does not include treatments.
                </p>
              {:else}
                <p class="px-1 text-xs text-muted-foreground">
                  Each prediction's context is what was going on in the three hours up to it.
                  {#if (detail.contexts ?? []).includes(PredictionContext.Adjusted)}
                    Override, temp target or exercise includes low "eating soon" temporary targets,
                    so some predictions made just before a meal sit there rather than after a meal.
                  {/if}
                  {#if anyInterrupted}
                    Interrupted counts are left out of the bias, MAE and n.
                  {/if}
                </p>
                <PredictionContextTable
                  curve={detail}
                  comparison={curveOf(comparison, detail.curve ?? PredictionCurve.Default)}
                  formatError={signed}
                  formatMae={unsigned}
                  {unit}
                />
              {/if}
            </div>
          </section>
        {/if}

        <p class="px-1 text-xs text-muted-foreground">
          {plural(kinds.length, ["# curve", "# curves"])} from the predictions your loop uploaded,
          each paired with the reading within 2.5 minutes of its horizon. Unmatched horizons had no
          reading; excluded ones fell in a sensor error or compression low.
        </p>
      {/if}
    {/if}
  </div>
{/if}
