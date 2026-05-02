<script lang="ts">
  import { Activity } from 'lucide-svelte';
  import * as Card from '$lib/components/ui/card';
  import { Button } from '$lib/components/ui/button';
  import * as ToggleGroup from '$lib/components/ui/toggle-group';
  import { Checkbox } from '$lib/components/ui/checkbox';
  import { Label } from '$lib/components/ui/label';
  import { Input } from '$lib/components/ui/input';

  type ViewMode = 'average' | 'spaghetti';

  type Props = {
    rangeDays: number;
    isMultiDay: boolean;
    timezone: string;
    viewMode: ViewMode;
    onViewModeChange: (m: ViewMode) => void;
    timeShift: boolean;
    onTimeShiftChange: (v: boolean) => void;
    tsWindowStart: string;
    tsWindowEnd: string;
    tsMinCarbs: number;
    onTimeShiftSettingsChange: (s: { startMin: number; endMin: number; minCarbs: number }) => void;
    showPredictions: boolean;
    onShowPredictionsChange: (v: boolean) => void;
    showApsBands: boolean;
    onShowApsBandsChange: (v: boolean) => void;
    showProfilesTable: boolean;
    onShowProfilesTableChange: (v: boolean) => void;
    onRefresh: () => void;
  };

  let {
    rangeDays,
    isMultiDay,
    timezone,
    viewMode,
    onViewModeChange,
    timeShift,
    onTimeShiftChange,
    tsWindowStart,
    tsWindowEnd,
    tsMinCarbs,
    onTimeShiftSettingsChange,
    showPredictions,
    onShowPredictionsChange,
    showApsBands,
    onShowApsBandsChange,
    showProfilesTable,
    onShowProfilesTableChange,
    onRefresh,
  }: Props = $props();

  let localStart = $state(tsWindowStart);
  let localEnd = $state(tsWindowEnd);
  let localMinCarbs = $state(tsMinCarbs);

  $effect(() => {
    localStart = tsWindowStart;
    localEnd = tsWindowEnd;
    localMinCarbs = tsMinCarbs;
  });

  function commitTimeShiftSettings() {
    const startMin = parseTime(localStart);
    const endMin = parseTime(localEnd);
    if (startMin == null || endMin == null || endMin <= startMin) return;
    onTimeShiftSettingsChange({ startMin, endMin, minCarbs: localMinCarbs });
  }

  function parseTime(t: string): number | null {
    const m = /^(\d{1,2}):(\d{2})$/.exec(t);
    if (!m) return null;
    const h = Number(m[1]);
    const mm = Number(m[2]);
    if (h < 0 || h > 24 || mm < 0 || mm > 59) return null;
    return h * 60 + mm;
  }
</script>

<Card.Root>
  <Card.Header class="pb-3">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <Card.Title class="flex items-center gap-2">
        <Activity class="h-5 w-5" />
        Loopalyzer
      </Card.Title>
      <div class="flex items-center gap-3 text-xs text-muted-foreground">
        <span>{rangeDays} {rangeDays === 1 ? 'day' : 'days'}</span>
        <span class="opacity-60">·</span>
        <span>{timezone}</span>
        <Button size="sm" variant="outline" onclick={onRefresh}>Refresh</Button>
      </div>
    </div>
  </Card.Header>
  <Card.Content class="pt-0 space-y-3">
    <div class="flex flex-wrap items-center gap-4">
      <ToggleGroup.Root
        type="single"
        value={viewMode}
        onValueChange={(v: string) => {
          if (v === 'average' || v === 'spaghetti') onViewModeChange(v);
        }}
        size="sm"
        variant="outline"
      >
        <ToggleGroup.Item value="average">Average + band</ToggleGroup.Item>
        <ToggleGroup.Item value="spaghetti">Spaghetti</ToggleGroup.Item>
      </ToggleGroup.Root>

      <div class="flex items-center gap-2">
        <Checkbox
          id="loopalyzer-predictions"
          checked={showPredictions}
          onCheckedChange={(v) => onShowPredictionsChange(v === true)}
        />
        <Label for="loopalyzer-predictions" class="text-sm">Predictions</Label>
      </div>
      <div class="flex items-center gap-2">
        <Checkbox
          id="loopalyzer-aps-bands"
          checked={showApsBands}
          onCheckedChange={(v) => onShowApsBandsChange(v === true)}
        />
        <Label for="loopalyzer-aps-bands" class="text-sm">APS bands</Label>
      </div>
      <div class="flex items-center gap-2">
        <Checkbox
          id="loopalyzer-profiles"
          checked={showProfilesTable}
          onCheckedChange={(v) => onShowProfilesTableChange(v === true)}
        />
        <Label for="loopalyzer-profiles" class="text-sm">Profiles table</Label>
      </div>
    </div>

    {#if isMultiDay}
      <div class="flex flex-wrap items-center gap-3 border-t pt-3">
        <div class="flex items-center gap-2">
          <Checkbox
            id="loopalyzer-time-shift"
            checked={timeShift}
            onCheckedChange={(v) => onTimeShiftChange(v === true)}
          />
          <Label for="loopalyzer-time-shift" class="text-sm">Time-shift to align meals</Label>
        </div>
        {#if timeShift}
          <div class="flex items-center gap-2 text-sm">
            <Label for="ts-start" class="text-muted-foreground">Window</Label>
            <Input
              id="ts-start"
              class="w-20 h-8"
              bind:value={localStart}
              onblur={commitTimeShiftSettings}
              placeholder="06:00"
            />
            <span class="text-muted-foreground">–</span>
            <Input
              id="ts-end"
              class="w-20 h-8"
              bind:value={localEnd}
              onblur={commitTimeShiftSettings}
              placeholder="20:00"
            />
          </div>
          <div class="flex items-center gap-2 text-sm">
            <Label for="ts-carbs" class="text-muted-foreground">Min carbs</Label>
            <Input
              id="ts-carbs"
              type="number"
              class="w-20 h-8"
              bind:value={localMinCarbs}
              onblur={commitTimeShiftSettings}
              min={0}
              step={1}
            />
            <span class="text-muted-foreground">g</span>
          </div>
        {/if}
      </div>
    {/if}
  </Card.Content>
</Card.Root>
