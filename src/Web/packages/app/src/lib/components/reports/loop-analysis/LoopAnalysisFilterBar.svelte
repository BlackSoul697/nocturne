<!--
  The filters every Loop Analysis tab shares. The range itself is the reports layout's; this bar
  adds the comparison, weekdays, exclusions and profile, and says when the range was cut short.
-->
<script lang="ts">
  import { DayOfWeek } from "$lib/api";
  import * as Select from "$lib/components/ui/select";
  import * as ToggleGroup from "$lib/components/ui/toggle-group";
  import { Checkbox } from "$lib/components/ui/checkbox";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import Clock from "@lucide/svelte/icons/clock";
  import Info from "@lucide/svelte/icons/info";
  import { formatLocale, formatMediumDateRange } from "$lib/utils/formatting";
  import {
    isCompareMode,
    MASK_KEYS,
    toWeekdays,
    type CompareMode,
    type LoopAnalysisFilterState,
    type RangeRequest,
  } from "./loop-analysis-filter";
  import { maskLabels } from "./filter-labels.svelte";

  interface Props {
    filter: LoopAnalysisFilterState;
    onchange: (next: LoopAnalysisFilterState) => void;
    primary: RangeRequest;
    comparison: RangeRequest | null;
    limitedTo24Hours: boolean;
    profileNames: string[];
  }

  let { filter, onchange, primary, comparison, limitedTo24Hours, profileNames }: Props = $props();

  const update = (patch: Partial<LoopAnalysisFilterState>) => onchange({ ...filter, ...patch });

  const compareLabels = $derived<Record<CompareMode, string>>({
    none: "No comparison",
    previous: "Previous period",
    custom: "Custom range",
  });

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
  const ALL_PROFILES = "__all__";
  const profiles = $derived(
    filter.profileName && !profileNames.includes(filter.profileName)
      ? [filter.profileName, ...profileNames]
      : profileNames
  );

  // The API's `to` is exclusive; the label names the last day it covers.
  const lastDay = (range: RangeRequest) => new Date(range.to.getTime() - 1);
</script>

<div class="space-y-3 rounded-lg border bg-card p-3 @md:p-4 print:hidden">
  <div class="flex flex-wrap items-end gap-x-6 gap-y-3">
    <div class="space-y-1.5">
      <span class="text-xs font-medium text-muted-foreground">Range</span>
      <p class="text-sm tabular-nums">{formatMediumDateRange(primary.from, lastDay(primary))}</p>
    </div>

    <div class="space-y-1.5">
      <Label for="loop-analysis-compare" size="sm" variant="muted">Compare with</Label>
      <Select.Root
        type="single"
        value={limitedTo24Hours ? "none" : filter.compare}
        onValueChange={(value) => {
          if (isCompareMode(value)) update({ compare: value });
        }}
        disabled={limitedTo24Hours}
      >
        <Select.Trigger id="loop-analysis-compare" class="w-44" size="sm">
          {compareLabels[limitedTo24Hours ? "none" : filter.compare]}
        </Select.Trigger>
        <Select.Content>
          {#each Object.entries(compareLabels) as [value, label] (value)}
            <Select.Item {value} {label}>{label}</Select.Item>
          {/each}
        </Select.Content>
      </Select.Root>
    </div>

    {#if filter.compare === "custom" && !limitedTo24Hours}
      <div class="flex items-end gap-2">
        <div class="space-y-1.5">
          <Label for="loop-analysis-compare-from" size="sm" variant="muted">From</Label>
          <Input
            id="loop-analysis-compare-from"
            type="date"
            aria-describedby="loop-analysis-compare-problem"
            size="sm" class="w-40"
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
            size="sm" class="w-40"
            value={filter.compareTo ?? ""}
            onchange={(e) => update({ compareTo: e.currentTarget.value || null })}
          />
        </div>
        <p id="loop-analysis-compare-problem" class="pb-1.5 text-xs text-warning" aria-live="polite">
          {#if !filter.compareFrom || !filter.compareTo}
            Pick both dates to compare.
          {:else if filter.compareFrom > filter.compareTo}
            The comparison has to end on or after the day it starts.
          {/if}
        </p>
      </div>
    {/if}

    {#if comparison}
      <div class="space-y-1.5">
        <span class="text-xs font-medium text-muted-foreground">Compared range</span>
        <p class="text-sm tabular-nums text-muted-foreground">
          {formatMediumDateRange(comparison.from, lastDay(comparison))}
        </p>
      </div>
    {/if}

    <div class="space-y-1.5">
      <Label for="loop-analysis-profile" size="sm" variant="muted">Profile</Label>
      <Select.Root
        type="single"
        value={filter.profileName ?? ALL_PROFILES}
        onValueChange={(value) => update({ profileName: value === ALL_PROFILES ? null : value })}
      >
        <Select.Trigger id="loop-analysis-profile" class="w-44" size="sm">
          {filter.profileName ?? "All profiles"}
        </Select.Trigger>
        <Select.Content>
          <Select.Item value={ALL_PROFILES} label="All profiles">All profiles</Select.Item>
          {#each profiles as name (name)}
            <Select.Item value={name} label={name}>{name}</Select.Item>
          {/each}
        </Select.Content>
      </Select.Root>
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
        onValueChange={(values: string[]) =>
          update({ weekdays: toWeekdays(values) })}
      >
        {#each weekdays as { day, label } (day)}
          <ToggleGroup.Item value={String(day)}>{label}</ToggleGroup.Item>
        {/each}
      </ToggleGroup.Root>
      {#if filter.weekdays.length === 0}
        <span class="text-xs text-muted-foreground">Every day</span>
      {/if}
    </div>

    <fieldset class="flex flex-wrap items-center gap-x-4 gap-y-2">
      <legend class="sr-only">Exclude</legend>
      <span aria-hidden="true" class="text-xs font-medium text-muted-foreground">Exclude</span>
      {#each MASK_KEYS as key (key)}
        <div class="flex items-center gap-1.5">
          <Checkbox
            id="loop-analysis-{key}"
            checked={filter[key]}
            onCheckedChange={(checked) => update({ [key]: checked === true })}
          />
          <Label for="loop-analysis-{key}" variant="option">{masks[key]}</Label>
        </div>
      {/each}
    </fieldset>
  </div>

  {#if limitedTo24Hours}
    <p class="flex items-start gap-2 text-xs text-muted-foreground">
      <Clock class="mt-0.5 size-3.5 shrink-0" />
      You can see the last 24 hours only, so the range starts 24 hours ago and nothing is compared.
    </p>
  {:else if primary.shortened || comparison?.shortened}
    <p class="flex items-start gap-2 text-xs text-muted-foreground">
      <Info class="mt-0.5 size-3.5 shrink-0" />
      Loop Analysis covers at most 90 days, so only the most recent 90 days of a longer range are
      analysed.
    </p>
  {/if}
</div>
