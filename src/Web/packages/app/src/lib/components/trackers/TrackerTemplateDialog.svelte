<script lang="ts">
  import * as Dialog from "$lib/components/ui/dialog";
  import { Button } from "$lib/components/ui/button";
  import { Label } from "$lib/components/ui/label";
  import { Checkbox } from "$lib/components/ui/checkbox";
  import { DurationInput } from "$lib/components/ui/duration-input";
  import { cn } from "$lib/utils";
  import {
    type AvailableTemplate,
    type TemplateResult,
  } from "$api";
  import {
    getTemplates,
    applyTemplate,
  } from "$lib/api/generated/trackerTemplates.generated.remote";
  import {
    ChevronRight,
    ChevronLeft,
    Check,
    Loader2,
    LayoutTemplate,
    AlertCircle,
    Activity,
    Radio,
    Package,
    Syringe,
    FlaskRound,
    Cable,
    Droplets,
  } from "lucide-svelte";

  const ICONS: Record<string, typeof Package> = {
    activity: Activity,
    radio: Radio,
    package: Package,
    syringe: Syringe,
    "flask-round": FlaskRound,
    cable: Cable,
    droplets: Droplets,
  };

  type Step = "select" | "configure" | "result";

  interface TrackerTemplateDialogProps {
    open: boolean;
    onClose: () => void;
    onApplied?: () => void;
  }

  let {
    open = $bindable(false),
    onClose,
    onApplied,
  }: TrackerTemplateDialogProps = $props();

  // Step state
  let step = $state<Step>("select");

  // Fetched templates
  let templates = $state<AvailableTemplate[]>([]);
  let isLoading = $state(false);
  let fetchError = $state<string | null>(null);

  // Selection state — map of consumableCatalogId to whether it's selected
  let selected = $state<Record<string, boolean>>({});

  // Lifespan overrides — map of consumableCatalogId to custom lifespan
  let lifespanOverrides = $state<Record<string, number | undefined>>({});

  // Apply state
  let isApplying = $state(false);
  let applyError = $state<string | null>(null);
  let appliedResults = $state<TemplateResult[]>([]);

  const selectedTemplates = $derived(
    templates.filter((t) => selected[t.consumableCatalogId!])
  );

  const hasSelection = $derived(selectedTemplates.length > 0);

  // Reset state when dialog opens
  $effect(() => {
    if (open) {
      step = "select";
      selected = {};
      lifespanOverrides = {};
      applyError = null;
      appliedResults = [];
      fetchTemplates();
    }
  });

  async function fetchTemplates() {
    isLoading = true;
    fetchError = null;
    try {
      templates = await getTemplates();
    } catch (err) {
      fetchError = err instanceof Error ? err.message : "Failed to load templates";
      templates = [];
    } finally {
      isLoading = false;
    }
  }

  function toggleTemplate(id: string) {
    selected[id] = !selected[id];
  }

  function goToConfigure() {
    if (!hasSelection) return;
    step = "configure";
  }

  function goBackToSelect() {
    step = "select";
  }

  async function applyTemplates() {
    isApplying = true;
    applyError = null;
    appliedResults = [];

    try {
      const results: TemplateResult[] = [];

      for (const template of selectedTemplates) {
        const result = await applyTemplate({
          consumableCatalogId: template.consumableCatalogId,
          lifespanHoursOverride: lifespanOverrides[template.consumableCatalogId!],
        });
        results.push(result);
      }

      appliedResults = results;
      step = "result";
      onApplied?.();
    } catch (err) {
      applyError = err instanceof Error ? err.message : "Failed to apply templates";
    } finally {
      isApplying = false;
    }
  }

  function handleClose() {
    open = false;
    onClose();
  }

  function formatLifespan(hours: number): string {
    if (hours < 24) return `${hours}h`;
    const days = Math.floor(hours / 24);
    const h = hours % 24;
    return h > 0 ? `${days}d ${h}h` : `${days}d`;
  }
</script>

<Dialog.Root bind:open>
  <Dialog.Content class="sm:max-w-125">
    <Dialog.Header>
      <Dialog.Title>
        {#if step === "select"}
          Set Up Trackers
        {:else if step === "configure"}
          Configure Lifespans
        {:else}
          Trackers Created
        {/if}
      </Dialog.Title>
      <Dialog.Description>
        {#if step === "select"}
          Select templates based on your connected devices.
        {:else if step === "configure"}
          Adjust lifespans for selected trackers. Hard-cutoff items use a fixed lifespan.
        {:else}
          Your tracker definitions have been created and are ready to use.
        {/if}
      </Dialog.Description>
    </Dialog.Header>

    <div class="py-4">
      {#if step === "select"}
        <!-- Step 1: Template selection -->
        {#if isLoading}
          <div class="flex items-center justify-center py-12">
            <Loader2 class="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        {:else if fetchError}
          <div class="text-center py-8">
            <AlertCircle class="h-10 w-10 mx-auto mb-3 text-muted-foreground opacity-50" />
            <p class="text-sm text-muted-foreground">{fetchError}</p>
            <Button variant="outline" size="sm" class="mt-4" onclick={fetchTemplates}>
              Retry
            </Button>
          </div>
        {:else if templates.length === 0}
          <div class="text-center py-8 text-muted-foreground">
            <LayoutTemplate class="h-10 w-10 mx-auto mb-3 opacity-50" />
            <p>No templates available</p>
            <p class="text-sm mt-1">
              Connect a device to see available tracker templates.
            </p>
          </div>
        {:else}
          <div class="space-y-2">
            {#each templates as template (template.consumableCatalogId)}
              {@const Glyph = ICONS[template.icon ?? 'package']}
              <button
                type="button"
                class={cn(
                  "flex w-full items-center gap-3 rounded-lg border p-3 text-left transition-colors",
                  selected[template.consumableCatalogId!]
                    ? "border-primary bg-primary/5"
                    : "hover:bg-muted/50"
                )}
                onclick={() => toggleTemplate(template.consumableCatalogId!)}
              >
                <Checkbox
                  checked={selected[template.consumableCatalogId!] ?? false}
                  onCheckedChange={() => toggleTemplate(template.consumableCatalogId!)}
                />
                <div class="p-1.5 rounded-md bg-muted">
                  <Glyph class="h-4 w-4" />
                </div>
                <div class="flex-1 min-w-0">
                  <div class="font-medium text-sm">{template.name}</div>
                  <div class="text-xs text-muted-foreground">
                    {#if template.deviceName}
                      {template.deviceName}
                    {/if}
                    {#if template.defaultLifespanHours}
                      {#if template.deviceName} · {/if}{formatLifespan(template.defaultLifespanHours)}
                    {/if}
                    {#if template.isHardCutoff}
                      · Fixed
                    {/if}
                  </div>
                </div>
              </button>
            {/each}
          </div>
        {/if}

      {:else if step === "configure"}
        <!-- Step 2: Lifespan configuration -->
        <div class="space-y-4">
          {#each selectedTemplates as template (template.consumableCatalogId)}
            {@const Glyph = ICONS[template.icon ?? 'package']}
            <div class="rounded-lg border p-4 space-y-3">
              <div class="flex items-center gap-3">
                <div class="p-1.5 rounded-md bg-muted">
                  <Glyph class="h-4 w-4" />
                </div>
                <div>
                  <div class="font-medium text-sm">{template.name}</div>
                  {#if template.deviceName}
                    <div class="text-xs text-muted-foreground">
                      {template.deviceName}
                    </div>
                  {/if}
                </div>
              </div>

              {#if template.isHardCutoff}
                <div class="text-xs text-muted-foreground bg-muted/50 rounded-md px-3 py-2">
                  Fixed lifespan of {formatLifespan(template.defaultLifespanHours!)} (cannot be changed)
                </div>
              {:else if template.defaultLifespanHours}
                <div class="space-y-2">
                  <Label class="text-xs">
                    Lifespan (default: {formatLifespan(template.defaultLifespanHours)})
                  </Label>
                  <DurationInput
                    bind:value={lifespanOverrides[template.consumableCatalogId!]}
                    placeholder="Leave blank for default"
                  />
                </div>
              {/if}
            </div>
          {/each}

          {#if applyError}
            <div class="rounded-md bg-destructive/10 border border-destructive/20 p-3 text-sm text-destructive">
              {applyError}
            </div>
          {/if}
        </div>

      {:else}
        <!-- Step 3: Results -->
        <div class="space-y-3">
          {#each appliedResults as result (result.trackerDefinitionId)}
            <div class="flex items-center gap-3 rounded-lg border p-3">
              <div class="flex h-8 w-8 items-center justify-center rounded-full bg-green-500/10">
                <Check class="h-4 w-4 text-green-600" />
              </div>
              <div>
                <div class="font-medium text-sm">{result.trackerDefinitionId}</div>
                <div class="text-xs text-muted-foreground">
                  Tracker and alarm rules created
                </div>
              </div>
            </div>
          {/each}
        </div>
      {/if}
    </div>

    <Dialog.Footer>
      {#if step === "select"}
        <Button variant="outline" onclick={handleClose}>
          Cancel
        </Button>
        <Button onclick={goToConfigure} disabled={!hasSelection}>
          Next
          <ChevronRight class="h-4 w-4 ml-1" />
        </Button>
      {:else if step === "configure"}
        <Button variant="outline" onclick={goBackToSelect} disabled={isApplying}>
          <ChevronLeft class="h-4 w-4 mr-1" />
          Back
        </Button>
        <Button onclick={applyTemplates} disabled={isApplying}>
          {#if isApplying}
            <Loader2 class="h-4 w-4 mr-2 animate-spin" />
            Applying...
          {:else}
            Apply {selectedTemplates.length} template{selectedTemplates.length > 1 ? "s" : ""}
          {/if}
        </Button>
      {:else}
        <Button onclick={handleClose}>
          Done
        </Button>
      {/if}
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>
