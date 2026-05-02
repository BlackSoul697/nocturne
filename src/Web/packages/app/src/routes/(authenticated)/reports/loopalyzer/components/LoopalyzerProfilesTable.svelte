<script lang="ts">
  import * as Card from '$lib/components/ui/card';
  import * as Table from '$lib/components/ui/table';
  import type { LoopalyzerProfile, LoopalyzerScheduleEntry } from '$lib/api/generated/nocturne-api-client';

  type Props = {
    profiles: ReadonlyArray<LoopalyzerProfile>;
  };

  let { profiles }: Props = $props();

  function formatRange(from?: string, to?: string | null): string {
    const f = from ? from.slice(0, 10) : '?';
    const t = to ? to.slice(0, 10) : 'present';
    return `${f} → ${t}`;
  }

  function formatSchedule(entries: ReadonlyArray<LoopalyzerScheduleEntry> | undefined): string {
    if (!entries || entries.length === 0) return '—';
    return entries.map((e) => `${e.time ?? '?'} ${(e.value ?? 0).toFixed(2)}`).join(', ');
  }
</script>

{#if profiles.length > 0}
  <Card.Root>
    <Card.Header class="pb-2">
      <Card.Title class="text-base">Profiles in range</Card.Title>
      <Card.Description>
        {profiles.length} profile{profiles.length === 1 ? '' : 's'} active during the selected window.
      </Card.Description>
    </Card.Header>
    <Card.Content>
      <Table.Root>
        <Table.Header>
          <Table.Row>
            <Table.Head>Name</Table.Head>
            <Table.Head>Validity</Table.Head>
            <Table.Head class="text-right">DIA (h)</Table.Head>
            <Table.Head>Basal (U/h)</Table.Head>
            <Table.Head>ISF</Table.Head>
            <Table.Head>IC</Table.Head>
          </Table.Row>
        </Table.Header>
        <Table.Body>
          {#each profiles as p, i (i)}
            <Table.Row>
              <Table.Cell class="font-medium">{p.name ?? '—'}</Table.Cell>
              <Table.Cell class="text-muted-foreground text-xs">
                {formatRange(p.validFrom, p.validTo)}
              </Table.Cell>
              <Table.Cell class="text-right tabular-nums">{(p.dia ?? 0).toFixed(1)}</Table.Cell>
              <Table.Cell class="text-xs tabular-nums">{formatSchedule(p.basal)}</Table.Cell>
              <Table.Cell class="text-xs tabular-nums">{formatSchedule(p.sensitivity)}</Table.Cell>
              <Table.Cell class="text-xs tabular-nums">{formatSchedule(p.carbRatio)}</Table.Cell>
            </Table.Row>
          {/each}
        </Table.Body>
      </Table.Root>
    </Card.Content>
  </Card.Root>
{/if}
