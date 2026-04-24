<script lang="ts">
  import { page } from "$app/state";
  import * as Card from "$lib/components/ui/card";
  import * as Tabs from "$lib/components/ui/tabs";
  import * as Table from "$lib/components/ui/table";
  import { Switch } from "$lib/components/ui/switch";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import {
    ScrollText,
    Settings2,
    ChevronLeft,
    ChevronRight,
    ChevronDown,
    ChevronUp,
    Loader2,
    Info,
  } from "lucide-svelte";
  import {
    getMutationAuditLog,
    getReadAccessAuditLog,
    getAuditConfig,
    updateAuditConfig,
  } from "$lib/api/generated/audits.generated.remote";

  // Permissions
  const effectivePermissions: string[] = $derived(
    (page.data as any).effectivePermissions ?? [],
  );
  const canManageAudit = $derived(
    effectivePermissions.includes("audit.manage") ||
      effectivePermissions.includes("*"),
  );

  // --- Config ---
  const configQuery = $derived(getAuditConfig());
  const config = $derived(configQuery.current);

  let readAuditEnabled = $state(false);
  let readRetentionDays = $state("");
  let mutationRetentionDays = $state("");
  let isSaving = $state(false);
  let configLoaded = $state(false);

  $effect(() => {
    if (config && !configLoaded) {
      readAuditEnabled = config.readAuditEnabled ?? false;
      readRetentionDays = config.readAuditRetentionDays?.toString() ?? "";
      mutationRetentionDays = config.mutationAuditRetentionDays?.toString() ?? "";
      configLoaded = true;
    }
  });

  async function saveConfig() {
    isSaving = true;
    try {
      await updateAuditConfig({
        readAuditEnabled,
        readAuditRetentionDays: readRetentionDays ? parseInt(readRetentionDays) : null,
        mutationAuditRetentionDays: mutationRetentionDays ? parseInt(mutationRetentionDays) : null,
      });
      configLoaded = false;
    } finally {
      isSaving = false;
    }
  }

  async function enableReadAudit() {
    isSaving = true;
    try {
      await updateAuditConfig({
        readAuditEnabled: true,
        readAuditRetentionDays: config?.readAuditRetentionDays ?? null,
        mutationAuditRetentionDays: config?.mutationAuditRetentionDays ?? null,
      });
      readAuditEnabled = true;
      configLoaded = false;
    } finally {
      isSaving = false;
    }
  }

  // --- Tab state ---
  let activeTab = $state("mutations");

  // --- Mutation log ---
  let mFrom = $state(
    new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString().split("T")[0],
  );
  let mTo = $state(new Date().toISOString().split("T")[0]);
  let mEntityType = $state("");
  let mAction = $state("");
  let mPageSize = $state(25);
  let mOffset = $state(0);
  let expandedMutation = $state<string | null>(null);

  const mutationsQuery = $derived(
    getMutationAuditLog({
      from: new Date(mFrom),
      to: new Date(mTo + "T23:59:59"),
      limit: mPageSize,
      offset: mOffset,
      sort: "created_at_desc",
      entityType: mEntityType || undefined,
      action: mAction || undefined,
    }),
  );
  const mutationsResult = $derived(mutationsQuery.current);
  const mutations = $derived((mutationsResult as any)?.data ?? []);
  const mutationsPagination = $derived((mutationsResult as any)?.pagination);
  const mutationsTotal = $derived(mutationsPagination?.total ?? 0);
  const mutationsPage = $derived(Math.floor(mOffset / mPageSize) + 1);
  const mutationsTotalPages = $derived(
    Math.max(1, Math.ceil(mutationsTotal / mPageSize)),
  );

  // --- Read access log ---
  let rFrom = $state(
    new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString().split("T")[0],
  );
  let rTo = $state(new Date().toISOString().split("T")[0]);
  let rEntityType = $state("");
  let rEndpoint = $state("");
  let rPageSize = $state(25);
  let rOffset = $state(0);
  let expandedRead = $state<string | null>(null);

  const readsQuery = $derived(
    getReadAccessAuditLog({
      from: new Date(rFrom),
      to: new Date(rTo + "T23:59:59"),
      limit: rPageSize,
      offset: rOffset,
      sort: "created_at_desc",
      entityType: rEntityType || undefined,
      endpoint: rEndpoint || undefined,
    }),
  );
  const readsResult = $derived(readsQuery.current);
  const reads = $derived((readsResult as any)?.data ?? []);
  const readsPagination = $derived((readsResult as any)?.pagination);
  const readsTotal = $derived(readsPagination?.total ?? 0);
  const readsPage = $derived(Math.floor(rOffset / rPageSize) + 1);
  const readsTotalPages = $derived(
    Math.max(1, Math.ceil(readsTotal / rPageSize)),
  );

  // --- Helpers ---
  function formatTime(date: string | Date) {
    return new Intl.DateTimeFormat("en-US", {
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    }).format(new Date(date));
  }

  function truncateId(id: string) {
    return id?.substring(0, 8) + "\u2026";
  }

  function actionVariant(
    action: string,
  ): "default" | "secondary" | "destructive" | "outline" {
    switch (action) {
      case "delete":
        return "destructive";
      case "create":
        return "default";
      default:
        return "secondary";
    }
  }

  function parseJson(json: string | null | undefined): Record<string, any> | null {
    if (!json) return null;
    try {
      return JSON.parse(json);
    } catch {
      return null;
    }
  }
</script>

<div class="space-y-6">
  <div class="flex items-center gap-3">
    <ScrollText class="h-6 w-6 text-muted-foreground" />
    <div>
      <h2 class="text-2xl font-bold tracking-tight">Audit Log</h2>
      <p class="text-sm text-muted-foreground">
        View data changes and access history for compliance.
      </p>
    </div>
  </div>

  <!-- Config Card (audit.manage only) -->
  {#if canManageAudit}
    <Card.Root>
      <Card.Header>
        <Card.Title class="flex items-center gap-2">
          <Settings2 class="h-4 w-4" />
          Audit Configuration
        </Card.Title>
      </Card.Header>
      <Card.Content>
        <div class="grid gap-4 sm:grid-cols-3">
          <div class="flex items-center gap-3">
            <Switch
              checked={readAuditEnabled}
              onCheckedChange={(v) => (readAuditEnabled = v === true)}
            />
            <Label>Read Access Logging</Label>
          </div>
          <div class="space-y-1">
            <Label for="read-retention">Read Log Retention (days)</Label>
            <Input
              id="read-retention"
              type="number"
              min="1"
              placeholder="Unlimited"
              bind:value={readRetentionDays}
            />
          </div>
          <div class="space-y-1">
            <Label for="mutation-retention">Mutation Log Retention (days)</Label>
            <Input
              id="mutation-retention"
              type="number"
              min="1"
              placeholder="Unlimited"
              bind:value={mutationRetentionDays}
            />
          </div>
        </div>
      </Card.Content>
      <Card.Footer>
        <Button onclick={saveConfig} disabled={isSaving}>
          {#if isSaving}
            <Loader2 class="mr-2 h-4 w-4 animate-spin" />
          {/if}
          Save Configuration
        </Button>
      </Card.Footer>
    </Card.Root>
  {/if}

  <!-- Tabbed Log Viewer -->
  <Tabs.Root bind:value={activeTab} class="space-y-4">
    <Tabs.List class="grid w-full grid-cols-2">
      <Tabs.Trigger value="mutations">Data Changes</Tabs.Trigger>
      <Tabs.Trigger value="reads">Data Access</Tabs.Trigger>
    </Tabs.List>

    <!-- === Mutation Audit Log Tab === -->
    <Tabs.Content value="mutations" class="space-y-4">
      <!-- Filters -->
      <div class="flex flex-wrap items-end gap-3">
        <div class="space-y-1">
          <Label for="m-from">From</Label>
          <Input id="m-from" type="date" bind:value={mFrom} onchange={() => (mOffset = 0)} />
        </div>
        <div class="space-y-1">
          <Label for="m-to">To</Label>
          <Input id="m-to" type="date" bind:value={mTo} onchange={() => (mOffset = 0)} />
        </div>
        <div class="space-y-1">
          <Label for="m-entity">Entity Type</Label>
          <Input
            id="m-entity"
            placeholder="e.g. SensorGlucose"
            bind:value={mEntityType}
            onchange={() => (mOffset = 0)}
          />
        </div>
        <div class="space-y-1">
          <Label for="m-action">Action</Label>
          <select
            id="m-action"
            class="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm"
            bind:value={mAction}
            onchange={() => (mOffset = 0)}
          >
            <option value="">All</option>
            <option value="create">Create</option>
            <option value="update">Update</option>
            <option value="delete">Delete</option>
            <option value="restore">Restore</option>
          </select>
        </div>
      </div>

      <!-- Table -->
      <div class="rounded-md border">
        <Table.Root>
          <Table.Header>
            <Table.Row>
              <Table.Head class="w-8"></Table.Head>
              <Table.Head>Time</Table.Head>
              <Table.Head>Subject</Table.Head>
              <Table.Head>Action</Table.Head>
              <Table.Head>Entity Type</Table.Head>
              <Table.Head>Entity ID</Table.Head>
              <Table.Head>Endpoint</Table.Head>
              <Table.Head>IP Address</Table.Head>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {#each mutations as entry (entry.id)}
              <Table.Row
                class="cursor-pointer"
                onclick={() =>
                  (expandedMutation =
                    expandedMutation === entry.id ? null : entry.id)}
              >
                <Table.Cell>
                  {#if expandedMutation === entry.id}
                    <ChevronUp class="h-4 w-4 text-muted-foreground" />
                  {:else}
                    <ChevronDown class="h-4 w-4 text-muted-foreground" />
                  {/if}
                </Table.Cell>
                <Table.Cell class="whitespace-nowrap text-sm">
                  {formatTime(entry.createdAt)}
                </Table.Cell>
                <Table.Cell>
                  {entry.subjectName ?? truncateId(entry.subjectId ?? "system")}
                </Table.Cell>
                <Table.Cell>
                  <Badge variant={actionVariant(entry.action)}>
                    {entry.action}
                  </Badge>
                </Table.Cell>
                <Table.Cell>{entry.entityType}</Table.Cell>
                <Table.Cell class="font-mono text-xs">
                  {truncateId(entry.entityId)}
                </Table.Cell>
                <Table.Cell class="text-xs text-muted-foreground">
                  {entry.endpoint ?? ""}
                </Table.Cell>
                <Table.Cell class="text-xs text-muted-foreground">
                  {entry.ipAddress ?? ""}
                </Table.Cell>
              </Table.Row>
              {#if expandedMutation === entry.id}
                <Table.Row>
                  <Table.Cell colspan={8}>
                    <div class="bg-muted/50 p-4 text-sm space-y-2 rounded">
                      {#if entry.reason}
                        <div>
                          <span class="font-medium">Reason:</span>
                          {entry.reason}
                        </div>
                      {/if}
                      {#if entry.authType}
                        <div>
                          <span class="font-medium">Auth:</span>
                          {entry.authType}
                        </div>
                      {/if}
                      {#if parseJson(entry.changes)}
                        {@const changes = parseJson(entry.changes)}
                        <div class="font-medium">Changes:</div>
                        <div class="grid gap-1 pl-2">
                          {#each Object.entries(changes ?? {}) as [field, diff]}
                            <div class="font-mono text-xs">
                              <span class="font-semibold">{field}:</span>
                              {#if typeof diff === "object" && diff !== null && "old" in diff}
                                <span class="text-red-600 dark:text-red-400 line-through">{JSON.stringify(diff.old)}</span>
                                {" "}
                                <span class="text-green-600 dark:text-green-400">{JSON.stringify(diff.new)}</span>
                              {:else}
                                {JSON.stringify(diff)}
                              {/if}
                            </div>
                          {/each}
                        </div>
                      {:else if entry.changes}
                        <pre class="text-xs overflow-auto">{entry.changes}</pre>
                      {/if}
                    </div>
                  </Table.Cell>
                </Table.Row>
              {/if}
            {:else}
              <Table.Row>
                <Table.Cell colspan={8} class="text-center text-muted-foreground py-8">
                  No mutation audit records found for the selected period.
                </Table.Cell>
              </Table.Row>
            {/each}
          </Table.Body>
        </Table.Root>
      </div>

      <!-- Pagination -->
      <div class="flex items-center justify-between">
        <p class="text-sm text-muted-foreground">
          {mutationsTotal} total records
        </p>
        <div class="flex items-center gap-2">
          <select
            class="flex h-9 rounded-md border border-input bg-transparent px-2 py-1 text-sm"
            bind:value={mPageSize}
            onchange={() => (mOffset = 0)}
          >
            <option value={25}>25 / page</option>
            <option value={50}>50 / page</option>
            <option value={100}>100 / page</option>
          </select>
          <Button
            variant="outline"
            size="icon"
            disabled={mOffset === 0}
            onclick={() => (mOffset = Math.max(0, mOffset - mPageSize))}
          >
            <ChevronLeft class="h-4 w-4" />
          </Button>
          <span class="text-sm">
            Page {mutationsPage} of {mutationsTotalPages}
          </span>
          <Button
            variant="outline"
            size="icon"
            disabled={mOffset + mPageSize >= mutationsTotal}
            onclick={() => (mOffset = mOffset + mPageSize)}
          >
            <ChevronRight class="h-4 w-4" />
          </Button>
        </div>
      </div>
    </Tabs.Content>

    <!-- === Read Access Log Tab === -->
    <Tabs.Content value="reads" class="space-y-4">
      {#if !config?.readAuditEnabled && !readAuditEnabled}
        <!-- Empty state: read audit not enabled -->
        <Card.Root>
          <Card.Content class="flex flex-col items-center justify-center py-12 text-center">
            <Info class="h-10 w-10 text-muted-foreground mb-4" />
            <p class="text-lg font-medium mb-2">Read audit is not enabled</p>
            {#if canManageAudit}
              <p class="text-sm text-muted-foreground mb-4">
                Enable read access logging to track who views patient data.
              </p>
              <Button onclick={enableReadAudit} disabled={isSaving}>
                {#if isSaving}
                  <Loader2 class="mr-2 h-4 w-4 animate-spin" />
                {/if}
                Enable now
              </Button>
            {:else}
              <p class="text-sm text-muted-foreground">
                Contact your admin to enable read audit logging.
              </p>
            {/if}
          </Card.Content>
        </Card.Root>
      {:else}
        <!-- Filters -->
        <div class="flex flex-wrap items-end gap-3">
          <div class="space-y-1">
            <Label for="r-from">From</Label>
            <Input id="r-from" type="date" bind:value={rFrom} onchange={() => (rOffset = 0)} />
          </div>
          <div class="space-y-1">
            <Label for="r-to">To</Label>
            <Input id="r-to" type="date" bind:value={rTo} onchange={() => (rOffset = 0)} />
          </div>
          <div class="space-y-1">
            <Label for="r-entity">Entity Type</Label>
            <Input
              id="r-entity"
              placeholder="e.g. SensorGlucose"
              bind:value={rEntityType}
              onchange={() => (rOffset = 0)}
            />
          </div>
          <div class="space-y-1">
            <Label for="r-endpoint">Endpoint</Label>
            <Input
              id="r-endpoint"
              placeholder="e.g. /api/v4/sensor-glucoses"
              bind:value={rEndpoint}
              onchange={() => (rOffset = 0)}
            />
          </div>
        </div>

        <!-- Table -->
        <div class="rounded-md border">
          <Table.Root>
            <Table.Header>
              <Table.Row>
                <Table.Head class="w-8"></Table.Head>
                <Table.Head>Time</Table.Head>
                <Table.Head>Subject</Table.Head>
                <Table.Head>Endpoint</Table.Head>
                <Table.Head>Entity Type</Table.Head>
                <Table.Head>Records</Table.Head>
                <Table.Head>Status</Table.Head>
                <Table.Head>IP Address</Table.Head>
              </Table.Row>
            </Table.Header>
            <Table.Body>
              {#each reads as entry (entry.id)}
                <Table.Row
                  class="cursor-pointer"
                  onclick={() =>
                    (expandedRead =
                      expandedRead === entry.id ? null : entry.id)}
                >
                  <Table.Cell>
                    {#if expandedRead === entry.id}
                      <ChevronUp class="h-4 w-4 text-muted-foreground" />
                    {:else}
                      <ChevronDown class="h-4 w-4 text-muted-foreground" />
                    {/if}
                  </Table.Cell>
                  <Table.Cell class="whitespace-nowrap text-sm">
                    {formatTime(entry.createdAt)}
                  </Table.Cell>
                  <Table.Cell>
                    {entry.subjectName ?? entry.apiSecretHashPrefix ?? "anonymous"}
                  </Table.Cell>
                  <Table.Cell class="text-xs font-mono">
                    {entry.endpoint}
                  </Table.Cell>
                  <Table.Cell>{entry.entityType ?? ""}</Table.Cell>
                  <Table.Cell>{entry.recordCount ?? ""}</Table.Cell>
                  <Table.Cell>
                    <Badge
                      variant={entry.statusCode >= 200 && entry.statusCode < 300
                        ? "default"
                        : entry.statusCode === 404
                          ? "secondary"
                          : "destructive"}
                    >
                      {entry.statusCode}
                    </Badge>
                  </Table.Cell>
                  <Table.Cell class="text-xs text-muted-foreground">
                    {entry.ipAddress ?? ""}
                  </Table.Cell>
                </Table.Row>
                {#if expandedRead === entry.id}
                  <Table.Row>
                    <Table.Cell colspan={8}>
                      <div class="bg-muted/50 p-4 text-sm space-y-2 rounded">
                        {#if entry.authType}
                          <div>
                            <span class="font-medium">Auth:</span>
                            {entry.authType}
                          </div>
                        {/if}
                        {#if entry.apiSecretHashPrefix}
                          <div>
                            <span class="font-medium">API Secret:</span>
                            {entry.apiSecretHashPrefix}...
                          </div>
                        {/if}
                        {#if parseJson(entry.queryParameters)}
                          {@const params = parseJson(entry.queryParameters)}
                          <div class="font-medium">Query Parameters:</div>
                          <div class="grid gap-1 pl-2">
                            {#each Object.entries(params ?? {}) as [key, value]}
                              <div class="font-mono text-xs">
                                <span class="font-semibold">{key}:</span>
                                {value}
                              </div>
                            {/each}
                          </div>
                        {/if}
                      </div>
                    </Table.Cell>
                  </Table.Row>
                {/if}
              {:else}
                <Table.Row>
                  <Table.Cell colspan={8} class="text-center text-muted-foreground py-8">
                    No read access records found for the selected period.
                  </Table.Cell>
                </Table.Row>
              {/each}
            </Table.Body>
          </Table.Root>
        </div>

        <!-- Pagination -->
        <div class="flex items-center justify-between">
          <p class="text-sm text-muted-foreground">
            {readsTotal} total records
          </p>
          <div class="flex items-center gap-2">
            <select
              class="flex h-9 rounded-md border border-input bg-transparent px-2 py-1 text-sm"
              bind:value={rPageSize}
              onchange={() => (rOffset = 0)}
            >
              <option value={25}>25 / page</option>
              <option value={50}>50 / page</option>
              <option value={100}>100 / page</option>
            </select>
            <Button
              variant="outline"
              size="icon"
              disabled={rOffset === 0}
              onclick={() => (rOffset = Math.max(0, rOffset - rPageSize))}
            >
              <ChevronLeft class="h-4 w-4" />
            </Button>
            <span class="text-sm">
              Page {readsPage} of {readsTotalPages}
            </span>
            <Button
              variant="outline"
              size="icon"
              disabled={rOffset + rPageSize >= readsTotal}
              onclick={() => (rOffset = rOffset + rPageSize)}
            >
              <ChevronRight class="h-4 w-4" />
            </Button>
          </div>
        </div>
      {/if}
    </Tabs.Content>
  </Tabs.Root>
</div>
