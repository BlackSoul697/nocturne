<!--
  The filters every Loop Analysis tab shares. The range picked is the reports layout's; this bar
  adds the comparison, weekdays, exclusions and profile. Once a response is in, it shows the range,
  comparison and filters the API resolved and applied rather than what was asked for; while a
  newer request is loading, an echo it has not answered yet is shown as loading, not stale.
-->
<script lang="ts">
  import {
    DayOfWeek,
    LoopAnalysisComparison,
    type LoopAnalysisMasks,
    type LoopAnalysisResolvedRange,
    type LoopAnalysisUnavailableFilter,
  } from "$lib/api";
  import * as Select from "$lib/components/ui/select";
  import * as ToggleGroup from "$lib/components/ui/toggle-group";
  import { Checkbox } from "$lib/components/ui/checkbox";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import Clock from "@lucide/svelte/icons/clock";
  import Info from "@lucide/svelte/icons/info";
  import { formatLocale, formatMediumDateRange } from "$lib/utils/formatting";
  import { startOfDay } from "$lib/utils/date-range";
  import {
    customComparisonProblem,
    MASK_KEYS,
    toComparison,
    toWeekdays,
    type LoopAnalysisFilterState,
    type MaskKey,
  } from "./loop-analysis-filter";
  import { maskLabels, missingScopeReason } from "./filter-labels.svelte";

  interface Props {
    filter: LoopAnalysisFilterState;
    onchange: (next: LoopAnalysisFilterState) => void;
    /** The range picked, shown until a response resolves it. */
    requested: { startDate: string; endDate: string };
    primaryRange?: LoopAnalysisResolvedRange | null;
    comparisonRange?: LoopAnalysisResolvedRange | null;
    /** The filters the last response applied, after the API's defaults and the caller's scopes. */
    applied?: LoopAnalysisMasks | null;
    /**
     * Nothing is loading: the echoes above answer the request on screen, or it failed and the bar
     * shows only what was picked.
     */
    settled: boolean;
    /** The caller reads only the last 24 hours, whatever range is picked. */
    clamped: boolean;
    /** The filters the caller's scopes do not let it use, as the last response reported them. */
    unavailable?: LoopAnalysisUnavailableFilter[] | null;
    profileNames: string[];
  }

  let {
    filter,
    onchange,
    requested,
    primaryRange = null,
    comparisonRange = null,
    applied = null,
    settled,
    clamped,
    unavailable = null,
    profileNames,
  }: Props = $props();

  const update = (patch: Partial<LoopAnalysisFilterState>) => onchange({ ...filter, ...patch });

  const compareLabels = $derived<Record<LoopAnalysisComparison, string>>({
    [LoopAnalysisComparison.None]: "No comparison",
    [LoopAnalysisComparison.Previous]: "Previous period",
    [LoopAnalysisComparison.Custom]: "Custom range",
  });
  const compare = $derived(clamped ? LoopAnalysisComparison.None : filter.compare);
  const compareProblem = $derived(clamped ? null : customComparisonProblem(filter));

  const weekdays = $derived(
    [
      DayOfWeek.Monday,
      DayOfWeek.Tuesday,
      DayOfWeek.Wednesday,
      DayOfWeek.Thursday,
      DayOfWeek.Friday,
      DayOfWeek.Saturday,
      DayOfWeek.Sunday,
    ].map((day) => ({
      day,
      // 4 January 2026 is a Sunday, so day d falls on the 4th + d.
      label: new Date(2026, 0, 4 + day).toLocaleDateString(formatLocale(), { weekday: "short" }),
    }))
  );

  const masks = $derived(maskLabels());
  const requiredScope = $derived(
    new Map((unavailable ?? []).map((f) => [f.filter ?? "", f.requiredScope ?? ""]))
  );
  const usable = (key: MaskKey | "profileName") => !requiredScope.has(key);
  const reason = (key: MaskKey | "profileName") => missingScopeReason(requiredScope.get(key) ?? "");

  // What the URL sets is shown as set; anything else is the API's echo, once it has answered.
  const maskLoading = (key: MaskKey) => filter.masks[key] == null && !settled;
  const maskChecked = (key: MaskKey) => filter.masks[key] ?? (settled ? applied?.[key] : false) ?? false;

  const ALL_PROFILES = "__all__";
  const profileName = $derived(filter.profileName ?? (settled ? applied?.profileName : null) ?? null);
  const shownPrimary = $derived(settled ? primaryRange : null);
  const shownComparison = $derived(settled ? comparisonRange : null);
  const profiles = $derived(
    profileName && !profileNames.includes(profileName) ? [profileName, ...profileNames] : profileNames
  );

  const dayRange = (start: string | undefined, end: string | undefined) =>
    start && end ? formatMediumDateRange(startOfDay(start), startOfDay(end)) : "";
</script>

<div class="space-y-3 rounded-lg border bg-card p-3 @md:p-4 print:hidden">
  <div class="flex flex-wrap items-end gap-x-6 gap-y-3">
    <div class="space-y-1.5">
      <span class="text-xs font-medium text-muted-foreground">Range</span>
      <p class="text-sm tabular-nums">
        {#if clamped && shownPrimary}
          Last 24 hours, {dayRange(shownPrimary.startDate, shownPrimary.endDate)}
        {:else if clamped}
          Last 24 hours
        {:else}
          {dayRange(shownPrimary?.startDate ?? requested.startDate, shownPrimary?.endDate ?? requested.endDate)}
        {/if}
      </p>
    </div>

    <div class="space-y-1.5">
      <Label for="loop-analysis-compare" size="sm" variant="muted">Compare with</Label>
      <Select.Root
        type="single"
        value={compare}
        onValueChange={(value) => {
          const next = toComparison(value);
          if (next) update({ compare: next });
        }}
        disabled={clamped}
      >
        <Select.Trigger id="loop-analysis-compare" class="w-44" size="sm">
          {compareLabels[compare]}
        </Select.Trigger>
        <Select.Content>
          {#each Object.values(LoopAnalysisComparison) as value (value)}
            <Select.Item {value} label={compareLabels[value]}>{compareLabels[value]}</Select.Item>
          {/each}
        </Select.Content>
      </Select.Root>
    </div>

    {#if compare === LoopAnalysisComparison.Custom}
      <div class="flex items-end gap-2">
        <div class="space-y-1.5">
          <Label for="loop-analysis-compare-from" size="sm" variant="muted">From</Label>
          <Input
            id="loop-analysis-compare-from"
            type="date"
            aria-describedby="loop-analysis-compare-problem"
            size="sm"
            class="w-40"
            value={filter.compareFrom ?? ""}
            onchange={(e) => update({ compareFrom: e.currentTarget.value || null })}
          />
        </div>
        <div class="space-y-1.5">
          <Label for="loop-analysis-compare-to" size="sm" variant="muted">To</Label>
          <Input
            id="loop-analysis-compare-to"
            type="date"
            aria-describedby="loop-analysis-compare-problem"
            size="sm"
            class="w-40"
            value={filter.compareTo ?? ""}
            onchange={(e) => update({ compareTo: e.currentTarget.value || null })}
          />
        </div>
        <p id="loop-analysis-compare-problem" class="pb-1.5 text-xs text-warning" aria-live="polite">
          {#if compareProblem === "incomplete"}
            Pick both dates to compare.
          {:else if compareProblem === "reversed"}
            The comparison has to end on or after the day it starts.
          {/if}
        </p>
      </div>
    {/if}

    {#if shownComparison}
      <div class="space-y-1.5">
        <span class="text-xs font-medium text-muted-foreground">Compared range</span>
        <p class="text-sm tabular-nums text-muted-foreground">
          {dayRange(shownComparison.startDate, shownComparison.endDate)}
        </p>
      </div>
    {/if}

    <div class="space-y-1.5">
      <Label for="loop-analysis-profile" size="sm" variant="muted">Profile</Label>
      <Select.Root
        type="single"
        value={profileName ?? ALL_PROFILES}
        onValueChange={(value) => update({ profileName: value === ALL_PROFILES ? null : value })}
        disabled={!usable("profileName")}
      >
        <Select.Trigger id="loop-analysis-profile" class="w-44" size="sm">
          {profileName ?? "All profiles"}
        </Select.Trigger>
        <Select.Content>
          <Select.Item value={ALL_PROFILES} label="All profiles">All profiles</Select.Item>
          {#each profiles as name (name)}
            <Select.Item value={name} label={name}>{name}</Select.Item>
          {/each}
        </Select.Content>
      </Select.Root>
      {#if !usable("profileName")}
        <p class="text-xs text-muted-foreground">{reason("profileName")}</p>
      {/if}
    </div>
  </div>

  <div class="flex flex-wrap items-center gap-x-6 gap-y-3">
    <div class="flex flex-wrap items-center gap-2">
      <span id="loop-analysis-weekdays" class="text-xs font-medium text-muted-foreground">Days</span>
      <ToggleGroup.Root
        type="multiple"
        variant="outline"
        size="sm"
        aria-labelledby="loop-analysis-weekdays"
        value={filter.weekdays.map(String)}
        onValueChange={(values: string[]) => update({ weekdays: toWeekdays(values) })}
      >
        {#each weekdays as { day, label } (day)}
          <ToggleGroup.Item value={String(day)}>{label}</ToggleGroup.Item>
        {/each}
      </ToggleGroup.Root>
      {#if filter.weekdays.length === 0}
        <span class="text-xs text-muted-foreground">Every day</span>
      {/if}
    </div>

    <fieldset class="flex flex-wrap items-center gap-x-4 gap-y-2" aria-busy={!settled}>
      <legend class="sr-only">Exclude</legend>
      <span aria-hidden="true" class="text-xs font-medium text-muted-foreground">Exclude</span>
      {#if !settled}
        <span class="sr-only">Loading the exclusions the report applied</span>
      {/if}
      {#each MASK_KEYS as key (key)}
        {@const enabled = usable(key)}
        <div class="flex items-center gap-1.5">
          <Checkbox
            id="loop-analysis-{key}"
            checked={enabled && maskChecked(key)}
            disabled={!enabled || maskLoading(key)}
            aria-describedby={enabled ? undefined : `loop-analysis-${key}-reason`}
            onCheckedChange={(checked) => update({ masks: { ...filter.masks, [key]: checked === true } })}
          />
          <Label for="loop-analysis-{key}" variant="option">{masks[key]}</Label>
          {#if !enabled}
            <span id="loop-analysis-{key}-reason" class="text-xs text-muted-foreground">
              ({reason(key)})
            </span>
          {/if}
        </div>
      {/each}
    </fieldset>
  </div>

  {#if clamped}
    <p class="flex items-start gap-2 text-xs text-muted-foreground">
      <Clock class="mt-0.5 size-3.5 shrink-0" />
      You can see the last 24 hours only, so the range picked does not apply and nothing is compared.
    </p>
  {:else if shownPrimary?.shortened || shownComparison?.shortened}
    <p class="flex items-start gap-2 text-xs text-muted-foreground">
      <Info class="mt-0.5 size-3.5 shrink-0" />
      Loop Analysis covers at most 90 days, so only the most recent 90 days of a longer range are
      analysed.
    </p>
  {/if}
</div>
