<script lang="ts">
  import { page } from "$app/state";
  import * as Popover from "$lib/components/ui/popover";
  import { Button } from "$lib/components/ui/button";
  import {
    NotificationUrgency,
    TrackerCategory,
    type LegacyConsumableKind,
    type LegacyFallbackDto,
  } from "$lib/api";
  import { satisfiesScope } from "$lib/authorization/scopes";
  import { hiddenLegacyFallbacks } from "$lib/stores/appearance-store.svelte";
  import { formatLocale } from "$lib/utils/formatting";
  import { cn } from "$lib/utils";
  import Clock from "@lucide/svelte/icons/clock";
  import EyeOff from "@lucide/svelte/icons/eye-off";
  import Plus from "@lucide/svelte/icons/plus";
  import TriangleAlert from "@lucide/svelte/icons/triangle-alert";
  import { TrackerCategoryIcon } from "$lib/components/icons";
  import { reachedUrgency } from "$lib/components/trackers/schedule";
  import { formatAge, formatLifespan } from "$lib/components/trackers/age.svelte";

  type AlertLevel = "none" | "info" | "warn" | "hazard" | "urgent";

  interface Props {
    fallback: LegacyFallbackDto;
    /** Epoch milliseconds the reached level is judged against. */
    now: number;
    /** Opens the conversion with this kind selected. */
    onCreateTracker?: (kind: LegacyConsumableKind) => void;
    class?: string;
  }

  let { fallback, now, onCreateTracker, class: className }: Props = $props();

  let popoverOpen = $state(false);

  // The conversion and the hidden list both change what the tenant tracks, which the server
  // gates on alerts.readwrite.
  const canWrite = $derived(
    satisfiesScope(page.data.effectivePermissions ?? [], "alerts.readwrite")
  );

  const LEVELS: Record<NotificationUrgency, AlertLevel> = {
    [NotificationUrgency.Info]: "info",
    [NotificationUrgency.Warn]: "warn",
    [NotificationUrgency.Hazard]: "hazard",
    [NotificationUrgency.Urgent]: "urgent",
  };

  const level = $derived.by((): AlertLevel => {
    const urgency = reachedUrgency(fallback, now);
    return urgency ? LEVELS[urgency] : "none";
  });

  // eslint-disable-next-line shadcn/require-static-classes -- the tracker pill's shape, as TrackerPill draws it; PopoverTrigger renders unstyled.
  const pillClasses = $derived(
    cn(
      "relative inline-flex flex-col items-start rounded-md px-3 py-1 text-left whitespace-nowrap transition-colors cursor-pointer select-none overflow-hidden hover:bg-accent/50",
      className
    )
  );

  const valueTone: Record<AlertLevel, string> = {
    none: "",
    info: "text-severity-info",
    warn: "text-severity-warn",
    hazard: "text-severity-hazard",
    urgent: "text-severity-urgent",
  };

  const category = $derived(fallback.spec?.category ?? TrackerCategory.Custom);
  const label = $derived(fallback.spec?.name ?? "");
  const ageDisplay = $derived(formatAge(fallback.ageHours));

  function createTracker() {
    if (!fallback.kind) return;
    popoverOpen = false;
    onCreateTracker?.(fallback.kind);
  }

  function hide() {
    const kind = fallback.kind;
    if (!kind) return;
    popoverOpen = false;
    if (!hiddenLegacyFallbacks.current.includes(kind)) {
      hiddenLegacyFallbacks.current = [...hiddenLegacyFallbacks.current, kind];
    }
  }
</script>

<Popover.Root bind:open={popoverOpen}>
  <Popover.Trigger class={pillClasses} data-testid="legacy-age-pill">
    <span class="flex items-center gap-1 text-xs text-muted-foreground">
      <TrackerCategoryIcon {category} class="size-3" />
      {label}
    </span>
    <span class="flex items-center gap-1 text-sm font-medium tabular-nums {valueTone[level]}">
      {#if level === "warn" || level === "hazard" || level === "urgent"}
        <TriangleAlert class="size-3.5" aria-hidden="true" />
        <span class="sr-only">{level === "urgent" ? "Urgent:" : "Warning:"}</span>
      {/if}
      {ageDisplay}
    </span>
  </Popover.Trigger>
  <Popover.Content
    class="w-72 p-0"
    align="center"
    side="bottom"
    data-testid="legacy-age-pill-popover"
  >
    <div class="border-b border-border px-4 py-3">
      <h4 class="flex items-center gap-2 text-sm font-semibold">
        <TrackerCategoryIcon {category} class="h-4 w-4" />
        {label}
      </h4>
      <p class="text-xs text-muted-foreground">
        No tracker records this item. The age starts at the last change.
      </p>
    </div>
    <div class="space-y-2 px-4 py-3">
      <div class="flex items-center justify-between text-sm">
        <span class="flex items-center gap-1.5 text-muted-foreground">
          <Clock class="h-3.5 w-3.5" />
          Age
        </span>
        <span class="font-medium">{ageDisplay}</span>
      </div>
      {#if fallback.latestEventAt}
        <div class="flex items-center justify-between text-sm">
          <span class="text-muted-foreground">Last change</span>
          <span class="font-medium">
            {new Date(fallback.latestEventAt).toLocaleString(formatLocale(), {
              month: "short",
              day: "numeric",
              hour: "2-digit",
              minute: "2-digit",
            })}
          </span>
        </div>
      {/if}
      {#if fallback.spec?.lifespanHours}
        <div class="flex items-center justify-between text-sm">
          <span class="text-muted-foreground">Expected lifespan</span>
          <span class="font-medium">{formatLifespan(fallback.spec.lifespanHours)}</span>
        </div>
      {/if}
    </div>
    {#if canWrite}
      <div class="flex gap-2 border-t border-border p-2">
        <Button variant="outline" size="sm" class="flex-1" onclick={createTracker}>
          <Plus class="mr-2 h-4 w-4" />
          Create a tracker
        </Button>
        <Button variant="ghost" size="sm" onclick={hide}>
          <EyeOff class="mr-2 h-4 w-4" />
          Hide
        </Button>
      </div>
    {/if}
  </Popover.Content>
</Popover.Root>
