<!--
  The Settings evidence page body: the basal, ISF and carb ratio the user's own data shows, each block's
  observed value on its spread against the profile's step line, then every block with its
  confidence, why it is not higher, what was set aside and the episodes behind it. Every figure
  comes from the API as it is; these are observations, never advice.
-->
<script lang="ts">
  import {
    LoopAnalysisSeries,
    SettingsEvidenceConfidence,
    SettingsEvidenceUnavailableReason,
    SettingsEvidenceWithheldReason,
    type BasalEvidenceBlock,
    type BasalEvidenceDay,
    type CarbRatioEvidenceEpisode,
    type IsfEvidenceEpisode,
    type SettingsEvidenceFigure,
    type SettingsEvidenceReport,
  } from "$lib/api";
  import * as Alert from "$lib/components/ui/alert";
  import { glucoseUnits } from "$lib/stores/appearance-store.svelte";
  import { bg, bgDelta, bgLabel, convertToDisplayUnits, hourLabel } from "$lib/utils/formatting";
  import { Button } from "$lib/components/ui/button";
  import ArrowRight from "@lucide/svelte/icons/arrow-right";
  import Clock from "@lucide/svelte/icons/clock";
  import EyeOff from "@lucide/svelte/icons/eye-off";
  import Info from "@lucide/svelte/icons/info";
  import Stethoscope from "@lucide/svelte/icons/stethoscope";
  import TimeOfDayPanel from "./TimeOfDayPanel.svelte";
  import StripKey, { type StripKeyItem } from "./StripKey.svelte";
  import NoTimeZone from "./NoTimeZone.svelte";
  import IgnoredFiltersNotice from "./IgnoredFiltersNotice.svelte";
  import SettingsEvidenceMarks from "./SettingsEvidenceMarks.svelte";
  import SettingsEvidenceBlock from "./SettingsEvidenceBlock.svelte";
  import { TimeOfDayCursor } from "./time-of-day-cursor.svelte";
  import { atMinute } from "./daily-profile-view";
  import { blockAt, blockSpan, matchingBlock, valueDomain, type EpisodeColumn } from "./settings-evidence-view";
  import { confidenceLabel, hoursText, unavailableText, type SettingsEvidenceSection } from "./settings-evidence-labels.svelte";

  interface Props {
    response: SettingsEvidenceReport;
    /** Loop health's sensitivity ratio by hour, under the same filter. */
    sensitivityHref?: string;
  }

  let { response, sensitivityHref }: Props = $props();

  const clamped = $derived(response.withheldReason === SettingsEvidenceWithheldReason.HistoryClamped);
  const withheld = $derived(
    response.withheldReason === SettingsEvidenceWithheldReason.ReadScopes ||
      (response.withheldSeries ?? []).includes(LoopAnalysisSeries.SettingsEvidence)
  );
  const rules = $derived(response.rules);
  const primary = $derived(response.primary ?? null);
  const comparison = $derived(response.comparison ?? null);
  const comparing = $derived(comparison != null);

  const units = $derived(glucoseUnits.current);
  const isfDisplay = $derived((mgdlPerUnit: number) => convertToDisplayUnits(mgdlPerUnit, units));
  const isfFormat = $derived((v: number) => (units === "mmol" ? isfDisplay(v).toFixed(1) : String(Math.round(v))));
  const isfUnit = $derived(`${bgLabel()} per U`);
  /** Two decimals, with a value that rounds to zero read as 0.00 rather than -0.00. */
  const twoPlaces = (v: number) => (Math.round(v * 100) === 0 ? 0 : v).toFixed(2);
  const rate = twoPlaces;
  const ratio = (v: number) => v.toFixed(1);
  const u = (v: number | null | undefined) => (v == null ? "–" : twoPlaces(v));
  const glucose = (v: number | null | undefined) => (v == null ? "–" : String(bg(v)));
  const change = (v: number | null | undefined) => (v == null ? "–" : bgDelta(v));
  const signed = (v: number) => `${Math.round(v * 100) > 0 ? "+" : ""}${twoPlaces(v)}`;

  const COLOR = "var(--chart-2)";
  const cursor = new TimeOfDayCursor();

  // Start and end glucose share a column, as do the three IOB figures, so a table fits the report width.
  const glucoseSpan = (e: { startGlucose?: number | null; endGlucose?: number | null }) =>
    `${glucose(e.startGlucose)} → ${glucose(e.endGlucose)}`;
  const iobSpan = (e: { iobAtStart?: number | null; iobAtEnd?: number | null; residualIob?: number | null }) =>
    `${u(e.iobAtStart)} / ${u(e.iobAtEnd)} / ${u(e.residualIob)}`;
  const iobLabel = "IOB start / end / residual, U";

  const iobHint = () =>
    "Insulin on board here compares delivery with the schedule. It is negative when the loop gave less basal than the schedule.";

  const basalColumns: EpisodeColumn<BasalEvidenceDay>[] = $derived([
    { label: "Need, U/h", value: (d) => u(d.value) },
    { label: "Hours", value: (d) => String(d.hours ?? "–") },
    { label: "Scheduled, U/h", value: (d) => u(d.profile) },
    { label: "Loop correction, U/h", value: (d) => u(d.netLoopCorrection) },
    {
      label: "Carried insulin, U/h",
      value: (d) => u(d.carriedInsulin),
      hint: "Insulin carried in from earlier, compared with the schedule. It is negative when the delivery before the period was less than the schedule.",
    },
    { label: `Glucose change, ${bgLabel()}`, value: (d) => change(d.glucoseChange) },
  ]);
  const isfColumns: EpisodeColumn<IsfEvidenceEpisode>[] = $derived([
    { label: `ISF, ${isfUnit}`, value: (e) => (e.value == null ? "–" : isfFormat(e.value)) },
    { label: "Bolus, U", value: (e) => u(e.units) },
    { label: `Glucose start → end, ${bgLabel()}`, value: (e) => glucoseSpan(e) },
    { label: "Net insulin, U", value: (e) => u(e.netInsulin) },
    { label: iobLabel, value: (e) => iobSpan(e), hint: iobHint(), wide: true },
    { label: "Basal need above schedule, U", value: (e) => u(e.basalExcess) },
    { label: "Insulin that acted, U", value: (e) => u(e.insulinActed) },
  ]);
  const crColumns: EpisodeColumn<CarbRatioEvidenceEpisode>[] = $derived([
    { label: "Carb ratio, g/U", value: (e) => (e.value == null ? "–" : ratio(e.value)) },
    { label: "Carbs, g", value: (e) => (e.grams == null ? "–" : String(e.grams)) },
    { label: `Glucose start → end, ${bgLabel()}`, value: (e) => glucoseSpan(e) },
    { label: "Net insulin, U", value: (e) => u(e.netInsulin) },
    { label: iobLabel, value: (e) => iobSpan(e), hint: iobHint(), wide: true },
    { label: "Basal need above schedule, U", value: (e) => u(e.basalExcess) },
    { label: "Insulin that acted, U", value: (e) => u(e.insulinActed) },
    { label: "Needed, U", value: (e) => u(e.insulinNeeded) },
    { label: `ISF used, ${isfUnit}`, value: (e) => (e.isf == null ? "–" : isfFormat(e.isf)) },
  ]);

  function basalDetails(block: BasalEvidenceBlock): string[] {
    if (block.meanNetLoopCorrection == null) return [];
    const lines = [`The loop's mean correction: ${signed(block.meanNetLoopCorrection)} U/h`];
    if (block.consistency != null) lines.push(`In the same direction on ${Math.round(block.consistency * 100)}% of days`);
    if (block.glucoseTrend?.mean != null) lines.push(`Glucose trend across the block: ${bgDelta(block.glucoseTrend.mean)} ${bgLabel()} per hour`);
    return lines;
  }

  interface Section {
    key: SettingsEvidenceSection;
    title: string;
    caption: string;
    direction: string;
    unit: string;
    blocks: SettingsEvidenceFigure[];
    compared: SettingsEvidenceFigure[] | null;
    format: (v: number) => string;
    display: (v: number) => number;
    floor: number;
  }

  const sections = $derived<Section[]>(
    primary == null
      ? []
      : [
          {
            key: "basal",
            title: "Basal",
            caption:
              "Fasting periods, in two-hour blocks. Each figure adds the schedule, the insulin that the loop added and the insulin carried in from earlier. It then adjusts for a glucose increase or decrease.",
            direction: "A higher basal rate means more insulin every hour.",
            unit: "U/h",
            blocks: primary.basal ?? [],
            compared: comparison?.basal ?? null,
            format: rate,
            display: (v) => v,
            floor: 0.5,
          },
          {
            key: "isf",
            title: "Insulin sensitivity (ISF)",
            caption: `Correction boluses given when glucose was high and steady, with no carbs logged near them. The figure is the glucose decrease in the next ${(rules?.isfMinimumWindowMs ?? 0) / 3_600_000} to ${hoursText(rules?.isfWindowMs)}, per unit of insulin that acted.`,
            direction: "A higher ISF means that each unit decreases glucose more, so less insulin is necessary for the same correction.",
            unit: isfUnit,
            blocks: primary.isf ?? [],
            compared: comparison?.isf ?? null,
            format: isfFormat,
            display: isfDisplay,
            floor: 40,
          },
          {
            key: "carbRatio",
            title: "Carb ratio",
            caption: `Meals logged with carbs and no other event near them. The figure is the grams for each unit of insulin that the meal needed in ${hoursText(rules?.carbRatioWindowMs)}.`,
            direction: "A higher carb ratio means less insulin for the same meal.",
            unit: "g/U",
            blocks: primary.carbRatio ?? [],
            compared: comparison?.carbRatio ?? null,
            format: ratio,
            display: (v) => v,
            floor: 10,
          },
        ]
  );

  const noMealsLogged = $derived(
    (primary?.carbRatio ?? []).length > 0 &&
      (primary?.carbRatio ?? []).every((b) => b.unavailableReason === SettingsEvidenceUnavailableReason.NoAnnouncedMeals)
  );

  function describe(section: Section, slot: number): string {
    const block = blockAt(section.blocks, slot);
    if (block == null) return "No block";
    if (block.observed == null) return unavailableText(block.unavailableReason, section.key);
    const read = `Observed ${section.format(block.observed)} ${section.unit}, profile ${block.profile == null ? "–" : section.format(block.profile)}, ${confidenceLabel(block.confidence)}, n ${block.n ?? 0}`;
    return block.towardsMoreInsulin ? `${read}. More insulin than your profile. See the note.` : read;
  }

  function key(section: Section): StripKeyItem[] {
    const observed = section.blocks.filter((b) => b.observed != null);
    return [
      { mark: "step", label: "Profile" },
      ...(observed.length > 0 ? [{ mark: "dot" as const, label: "Observed, middle half of episodes", color: COLOR }] : []),
      ...(observed.some((b) => b.confidence === SettingsEvidenceConfidence.Low) ? [{ mark: "dot-hollow" as const, label: "Low confidence", color: COLOR }] : []),
      ...(comparing ? [{ mark: "dot-compared" as const, label: "Compared range" }] : []),
      ...((section.compared ?? []).some((b) => b.observed != null && b.confidence === SettingsEvidenceConfidence.Low)
        ? [{ mark: "dot-compared-hollow" as const, label: "Compared range, low confidence" }]
        : []),
    ];
  }

  const spanText = (figure: SettingsEvidenceFigure) =>
    `${hourLabel(atMinute((figure.startHour ?? 0) * 60))}–${hourLabel(atMinute((figure.endHour ?? 0) * 60))}`;
  const ticks = (blocks: SettingsEvidenceFigure[]) => [...new Set(blocks.flatMap((b) => [blockSpan(b).start, blockSpan(b).end]))];
</script>

{#if response.timeZoneUnavailableReason}
  <NoTimeZone reason={response.timeZoneUnavailableReason} />
{:else}
  <div class="space-y-6">
    <Alert.Root variant="info">
      <Stethoscope />
      <Alert.Title>Observations, not advice</Alert.Title>
      <Alert.Description>
        These figures come from your own data. Each figure shows its confidence. They are not medical
        advice. Speak to your diabetes care team before you change a setting.
      </Alert.Description>
    </Alert.Root>

    <IgnoredFiltersNotice ignored={response.ignoredFilters} />

    {#if clamped}
      <div class="flex flex-col items-center gap-3 rounded-lg border border-dashed px-6 py-10 text-center text-muted-foreground">
        <Clock class="size-10 opacity-50" />
        <p class="font-medium text-foreground">Settings evidence needs more than the last 24 hours of history</p>
        <p class="max-w-prose text-sm">
          You can see only the most recent 24 hours of this data. Each figure on this page is checked
          against the hours before it, for example whether food was eaten. You cannot see those hours.
          So this page does not show figures that it cannot check.
        </p>
      </div>
    {:else if withheld || primary == null}
      <div class="flex flex-col items-center gap-3 rounded-lg border border-dashed px-6 py-10 text-center text-muted-foreground">
        <EyeOff class="size-10 opacity-50" />
        <p class="font-medium text-foreground">Your access does not include the data for this page</p>
        <p class="max-w-prose text-sm">
          Settings evidence uses glucose, the delivered insulin and the profile settings together. It
          shows only if you have access to all three.
        </p>
      </div>
    {:else}
      {#each sections as section (section.key)}
        <section class="space-y-3 rounded-lg border bg-card p-3 @md:p-4" aria-labelledby="settings-evidence-{section.key}">
          <div class="space-y-1 px-1">
            <h3 id="settings-evidence-{section.key}" class="font-medium">{section.title}</h3>
            <p class="text-sm text-muted-foreground">{section.caption}</p>
            <p class="text-sm text-muted-foreground">{section.direction}</p>
            {#if section.key === "isf" && sensitivityHref}
              <Button href={sensitivityHref} variant="link" size="inline">
                The loop's own sensitivity ratio by hour, in Loop health
                <ArrowRight />
              </Button>
            {/if}
          </div>
          <div class="space-y-1">
            <TimeOfDayPanel
              id="settings-evidence-{section.key}-chart"
              title="{section.title} by time of day"
              unit={section.unit}
              {cursor}
              yDomain={valueDomain([...section.blocks, ...(section.compared ?? [])], section.floor, section.display)}
              heightClass="h-40"
              showTimeAxis
              ticks={ticks(section.blocks)}
              slotHeading={false}
              describe={(slot) => describe(section, slot)}
            >
              {#snippet marks()}
                <SettingsEvidenceMarks blocks={section.blocks} comparison={section.compared} color={COLOR} display={section.display} />
              {/snippet}
              {#snippet tooltip(slot)}
                {@const block = blockAt(section.blocks, slot)}
                {#if block}
                  {@const compared = matchingBlock(section.compared, block)}
                  <p class="font-medium">{spanText(block)}</p>
                  {#if block.observed == null}
                    <p class="text-muted-foreground">{unavailableText(block.unavailableReason, section.key)}</p>
                  {:else}
                    <dl class="grid grid-cols-[auto_auto] gap-x-3 tabular-nums">
                      <dt class="text-muted-foreground">Observed</dt>
                      <dd class="text-right">{section.format(block.observed)} {section.unit}</dd>
                      {#if block.spread?.p25 != null && block.spread?.p75 != null}
                        <dt class="text-muted-foreground">Middle half</dt>
                        <dd class="text-right">{section.format(block.spread.p25)}–{section.format(block.spread.p75)}</dd>
                      {/if}
                      <dt class="text-muted-foreground">Episodes</dt>
                      <dd class="text-right">{block.n ?? 0}</dd>
                      {#if comparing}
                        <dt class="text-muted-foreground">Compared</dt>
                        <dd class="text-right">
                          {#if compared?.observed == null}–{:else}{section.format(compared.observed)}, n {compared.n ?? 0}, {confidenceLabel(compared.confidence)}{/if}
                        </dd>
                      {/if}
                    </dl>
                    <p class="mt-1">{confidenceLabel(block.confidence)}</p>
                    {#if block.towardsMoreInsulin}
                      <p class="mt-1 flex items-center gap-1">
                        <Stethoscope class="size-3.5 shrink-0 text-info" aria-hidden="true" />
                        More insulin than your profile. See the note.
                      </p>
                    {/if}
                  {/if}
                  <p class="text-muted-foreground">Profile {block.profile == null ? "–" : section.format(block.profile)} {section.unit}</p>
                {/if}
              {/snippet}
            </TimeOfDayPanel>
            <StripKey items={key(section)} />
          </div>

          {#if section.key === "carbRatio" && noMealsLogged}
            <p class="rounded-md border px-3 py-2 text-sm text-muted-foreground">
              Carb ratio needs meals logged with carbs. No meals were logged with carbs in this range.
            </p>
          {:else}
          <div class="@container rounded-md border" role="list" aria-label="{section.title} by block">
            {#if section.key === "basal"}
              {#each primary.basal ?? [] as block (`${block.startHour}`)}
                <div role="listitem">
                  <SettingsEvidenceBlock
                    figure={block}
                    compared={matchingBlock(comparison?.basal, block)}
                    {comparing}
                    section={section.key}
                    format={section.format}
                    unit={section.unit}
                    episodes={block.days ?? []}
                    columns={basalColumns}
                    {rules}
                    details={basalDetails(block)}
                  />
                </div>
              {/each}
            {:else if section.key === "isf"}
              {#each primary.isf ?? [] as block (`${block.startHour}`)}
                <div role="listitem">
                  <SettingsEvidenceBlock
                    figure={block}
                    compared={matchingBlock(comparison?.isf, block)}
                    {comparing}
                    section={section.key}
                    format={section.format}
                    unit={section.unit}
                    episodes={block.episodes ?? []}
                    columns={isfColumns}
                    {rules}
                  />
                </div>
              {/each}
            {:else}
              {#each primary.carbRatio ?? [] as block (`${block.startHour}`)}
                <div role="listitem">
                  <SettingsEvidenceBlock
                    figure={block}
                    compared={matchingBlock(comparison?.carbRatio, block)}
                    {comparing}
                    section={section.key}
                    format={section.format}
                    unit={section.unit}
                    episodes={block.episodes ?? []}
                    columns={crColumns}
                    {rules}
                  />
                </div>
              {/each}
            {/if}
          </div>
          {/if}
        </section>
      {/each}

      <p class="flex items-center gap-1.5 px-1 text-xs text-muted-foreground">
        <Info class="size-3.5" />
        Hollow dots show figures with low confidence. Open a block to see the reasons, the candidates
        that were set aside and the episodes for the figure. Use the arrow keys to move along the day
        and between charts.
      </p>
    {/if}
  </div>
{/if}
