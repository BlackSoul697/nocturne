<script lang="ts">
  import type { Snippet } from "svelte";
  import { TimeZoneUnavailableReason } from "$lib/api";

  let { reason, children }: { reason: TimeZoneUnavailableReason; children: Snippet } = $props();
</script>

{#if reason === TimeZoneUnavailableReason.Share}
  A shared link cannot see the time zone this data belongs to,
{:else if reason === TimeZoneUnavailableReason.LookupFailed}
  The time zone for this data could not be loaded just now,
{:else if reason === TimeZoneUnavailableReason.Unrecognised}
  The time zone in the profile settings for this data isn't one this server
  recognises,
{:else}
  No time zone is set in the profile settings for this data,
{/if}
{@render children()}
{#if reason === TimeZoneUnavailableReason.NotConfigured}
  Setting a time zone in the profile settings fixes this.
{:else if reason === TimeZoneUnavailableReason.Unrecognised}
  Correcting the time zone in the profile settings fixes this.
{:else if reason === TimeZoneUnavailableReason.LookupFailed}
  Reloading the report later may fix this.
{/if}
