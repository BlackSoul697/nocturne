<!-- Why a time-of-day report has nothing to show when no tenant time zone resolved. -->
<script lang="ts">
  import { TimeZoneUnavailableReason } from "$lib/api";
  import Globe from "@lucide/svelte/icons/globe";

  let { reason }: { reason: TimeZoneUnavailableReason } = $props();
</script>

<div class="flex flex-col items-center gap-3 rounded-lg border border-dashed px-6 py-10 text-center">
  <Globe class="size-10 text-muted-foreground/50" />
  <h2 class="font-medium">This report needs a time zone</h2>
  <p class="max-w-prose text-sm text-muted-foreground">
    Loop Analysis aligns days by local time of day. It needs the time zone of the data to do this.
    {#if reason === TimeZoneUnavailableReason.Share}
      A shared link cannot see that time zone, so this report is not available here.
    {:else if reason === TimeZoneUnavailableReason.LookupFailed}
      The time zone did not load. To try again, reload the report later.
    {:else if reason === TimeZoneUnavailableReason.Unrecognised}
      This server does not recognise the time zone in the profile settings. To fix this, correct the
      time zone in the profile settings.
    {:else}
      The profile settings do not have a time zone. To fix this, set a time zone in the profile
      settings.
    {/if}
  </p>
</div>
