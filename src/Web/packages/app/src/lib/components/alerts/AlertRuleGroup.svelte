<script lang="ts">
  import type { AlertRuleResponse } from "$api-clients";
  import * as Collapsible from "$lib/components/ui/collapsible";
  import { Badge } from "$lib/components/ui/badge";
  import { ChevronRight } from "lucide-svelte";
  import AlertRuleRow from "./AlertRuleRow.svelte";

  interface Props {
    label: string;
    rules: AlertRuleResponse[];
    togglingRuleId: string | null;
    deletingRuleId: string | null;
    testingRuleId: string | null;
    onToggleEnabled: (ruleId: string) => void;
    onEdit: (rule: AlertRuleResponse) => void;
    onDelete: (ruleId: string) => void;
    onTestFire: (ruleId: string) => void;
    resolveAlertName?: (id: string) => string | undefined;
  }

  let {
    label,
    rules,
    togglingRuleId,
    deletingRuleId,
    testingRuleId,
    onToggleEnabled,
    onEdit,
    onDelete,
    onTestFire,
    resolveAlertName,
  }: Props = $props();

  let open = $state(false);

  let enabledCount = $derived(rules.filter((r) => r.isEnabled).length);
  let allEnabled = $derived(enabledCount === rules.length);
  let noneEnabled = $derived(enabledCount === 0);
</script>

<Collapsible.Root bind:open>
  <Collapsible.Trigger
    class="flex w-full items-center gap-3 rounded-md border bg-muted/40 px-4 py-3 text-left transition-colors hover:bg-muted/70 {noneEnabled ? 'opacity-60' : ''}"
  >
    <ChevronRight
      class="h-4 w-4 shrink-0 text-muted-foreground transition-transform duration-200 {open ? 'rotate-90' : ''}"
    />
    <div class="min-w-0 flex-1">
      <span class="text-sm font-semibold">{label}</span>
    </div>
    <div class="flex items-center gap-2 shrink-0">
      {#if noneEnabled}
        <Badge variant="secondary" class="text-[10px]">All disabled</Badge>
      {:else if !allEnabled}
        <Badge variant="secondary" class="text-[10px]">{enabledCount}/{rules.length} enabled</Badge>
      {/if}
      <Badge variant="outline" class="text-[10px] tabular-nums">{rules.length} {rules.length === 1 ? "rule" : "rules"}</Badge>
    </div>
  </Collapsible.Trigger>
  <Collapsible.Content>
    <div class="ml-4 mt-1 space-y-2 border-l-2 border-muted pl-3">
      {#each rules as rule (rule.id)}
        <AlertRuleRow
          {rule}
          isToggling={togglingRuleId === rule.id}
          isDeleting={deletingRuleId === rule.id}
          isTesting={testingRuleId === rule.id}
          onToggleEnabled={() => onToggleEnabled(rule.id ?? "")}
          onEdit={() => onEdit(rule)}
          onDelete={() => onDelete(rule.id ?? "")}
          onTestFire={() => onTestFire(rule.id ?? "")}
          {resolveAlertName}
        />
      {/each}
    </div>
  </Collapsible.Content>
</Collapsible.Root>
