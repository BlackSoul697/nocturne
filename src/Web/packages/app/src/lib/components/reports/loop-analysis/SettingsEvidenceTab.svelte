<!--
  The Settings evidence tab: the basal, ISF and carb ratio the user's own data shows, each block's
  observed value on its spread against the profile's step line, then every block with its
  confidence, why it is not higher, what was set aside and the episodes behind it. Every figure
  comes from the API as it is; these are observations, never advice.
-->
<script lang="ts">
  import {
    LoopAnalysisSeries,
    SettingsEvidenceConfidence,
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
  import Clock from "@lucide/svelte/icons/clock";
  import EyeOff from "@lucide/svelte/icons/eye-off";
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
  import { confidenceLabel, unavailableText, type SettingsEvidenceSection } from "./settings-evidence-labels.svelte";

  let { response }: { response: SettingsEvidenceReport } = $props();

  const withheld = $derived((response.withheldSeries ?? []).includes(LoopAnalysisSeries.SettingsEvidence));
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

  const basalColumns: EpisodeColumn<BasalEvidenceDay>[] = [
    { label: "Hours", value: (d) => String(d.hours ?? "–") },
    { label: "Need, U/h", value: (d) => u(d.value) },
    { label: "Scheduled, U/h", value: (d) => u(d.profile) },
    { label: "Loop correction, U/h", value: (d) => u(d.netLoopCorrection) },
    { label: "Carried insulin, U/h", value: (d) => u(d.carriedInsulin) },
    { label: "Glucose change", value: (d) => change(d.glucoseChange) },
  ];
  const isfColumns: EpisodeColumn<IsfEvidenceEpisode>[] = $derived([
    { label: "Bolus, U", value: (e) => u(e.units) },
    { label: "Start", value: (e) => glucose(e.startGlucose) },
    { label: "End", value: (e) => glucose(e.endGlucose) },
    { label: "Net insulin, U", value: (e) => u(e.netInsulin) },
    { label: "IOB start, U", value: (e) => u(e.iobAtStart) },
    { label: "IOB end, U", value: (e) => u(e.iobAtEnd) },
    { label: "Residual IOB, U", value: (e) => u(e.residualIob) },
    { label: "Basal excess, U", value: (e) => u(e.basalExcess) },
    { label: "Acted, U", value: (e) => u(e.insulinActed) },
    { label: `ISF, ${isfUnit}`, value: (e) => (e.value == null ? "–" : isfFormat(e.value)) },
  ]);
  const crColumns: EpisodeColumn<CarbRatioEvidenceEpisode>[] = $derived([
    { label: "Carbs, g", value: (e) => (e.grams == null ? "–" : String(e.grams)) },
    { label: "Start", value: (e) => glucose(e.startGlucose) },
    { label: "End", value: (e) => glucose(e.endGlucose) },
    { label: "Net insulin, U", value: (e) => u(e.netInsulin) },
    { label: "IOB start, U", value: (e) => u(e.iobAtStart) },
    { label: "IOB end, U", value: (e) => u(e.iobAtEnd) },
    { label: "Residual IOB, U", value: (e) => u(e.residualIob) },
    { label: "Basal excess, U", value: (e) => u(e.basalExcess) },
    { label: "Acted, U", value: (e) => u(e.insulinActed) },
    { label: "Needed, U", value: (e) => u(e.insulinNeeded) },
    { label: `ISF used, ${isfUnit}`, value: (e) => (e.isf == null ? "–" : isfFormat(e.isf)) },
    { label: "Carb ratio, g/U", value: (e) => (e.value == null ? "–" : ratio(e.value)) },
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
            caption: "Fasting stretches, in two-hour blocks: the schedule, what the loop added and what glucose's drift says was still missing.",
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
            caption: "Correction boluses given with glucose high and steady and nothing eaten: the drop over four hours per unit that acted.",
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
            caption: "Meals logged with carbs and nothing else nearby: grams per unit of insulin the meal needed over four hours.",
            unit: "g/U",
            blocks: primary.carbRatio ?? [],
            compared: comparison?.carbRatio ?? null,
            format: ratio,
            display: (v) => v,
            floor: 10,
          },
        ]
  );

  function describe(section: Section, slot: number): string {
    const block = blockAt(section.blocks, slot);
    if (block == null) return "No block";
    if (block.observed == null) return unavailableText(block.unavailableReason, section.key);
    return `Observed ${section.format(block.observed)} ${section.unit}, profile ${block.profile == null ? "–" : section.format(block.profile)}, ${confidenceLabel(block.confidence)}, n ${block.n ?? 0}`;
  }

  function key(section: Section): StripKeyItem[] {
    const blocks = section.blocks;
    return [
      { mark: "step", label: "Profile" },
      ...(blocks.some((b) => b.observed != null && b.confidence !== SettingsEvidenceConfidence.Low) ? [{ mark: "dot" as const, label: "Observed, middle half of episodes", color: COLOR }] : []),
      ...(blocks.some((b) => b.observed != null && b.confidence === SettingsEvidenceConfidence.Low) ? [{ mark: "dot-hollow" as const, label: "Low confidence", color: COLOR }] : []),
      ...(comparing ? [{ mark: "dot-compared" as const, label: "Compared range" }] : []),
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
        These figures are read from your own data, and each says how sure it can be. They are not
        medical advice. Talk any change to your settings through with your diabetes care team.
      </Alert.Description>
    </Alert.Root>

    {#if response.historyClamped}
      <Alert.Root variant="info">
        <Clock />
        <Alert.Title>Only the last 24 hours are included</Alert.Title>
        <Alert.Description>
          You can read only the most recent 24 hours of this data, so the evidence covers those 24
          hours whatever range is picked and compares nothing. That is rarely enough for a figure.
        </Alert.Description>
      </Alert.Root>
    {/if}

    <IgnoredFiltersNotice ignored={response.ignoredFilters} />

    {#if withheld || primary == null}
      <div class="flex flex-col items-center gap-3 rounded-lg border border-dashed px-6 py-10 text-center text-muted-foreground">
        <EyeOff class="size-10 opacity-50" />
        <p class="font-medium text-foreground">Your access does not include the data this tab is drawn from</p>
        <p class="max-w-prose text-sm">
          Settings evidence reads glucose, the insulin delivered and the profile's settings together,
          so it is shown only with access to all three.
        </p>
      </div>
    {:else}
      {#each sections as section (section.key)}
        <section class="space-y-3 rounded-lg border bg-card p-3 @md:p-4" aria-labelledby="settings-evidence-{section.key}">
          <div class="space-y-1 px-1">
            <h3 id="settings-evidence-{section.key}" class="font-medium">{section.title}</h3>
            <p class="text-sm text-muted-foreground">{section.caption}</p>
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
                        <dd class="text-right">{compared?.observed == null ? "–" : section.format(compared.observed)}</dd>
                      {/if}
                    </dl>
                    <p class="mt-1">{confidenceLabel(block.confidence)}</p>
                  {/if}
                  <p class="text-muted-foreground">Profile {block.profile == null ? "–" : section.format(block.profile)} {section.unit}</p>
                {/if}
              {/snippet}
            </TimeOfDayPanel>
            <StripKey items={key(section)} />
          </div>

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
                  />
                </div>
              {/each}
            {/if}
          </div>
        </section>
      {/each}

      <p class="flex items-center gap-1.5 px-1 text-xs text-muted-foreground">
        <EyeOff class="size-3.5" />
        Hollow dots are low-confidence figures. Open a block to see why, what was set aside, and the
        episodes behind it. With the keyboard, the arrow keys move along the day and between charts.
      </p>
    {/if}
  </div>
{/if}
