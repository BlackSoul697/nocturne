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
    Loop Analysis lines days up by local time of day, and a day cannot be placed without the time
    zone the data belongs to.
    {#if reason === TimeZoneUnavailableReason.Share}
      A shared link cannot see that time zone, so this report is not available here.
    {:else if reason === TimeZoneUnavailableReason.LookupFailed}
      The time zone could not be loaded just now. Reloading the report later may fix this.
    {:else if reason === TimeZoneUnavailableReason.Unrecognised}
      The time zone in the profile settings is not one this server recognises. Correcting it in
      the profile settings fixes this.
    {:else}
      No time zone is set in the profile settings. Setting one there fixes this.
    {/if}
  </p>
</div>
