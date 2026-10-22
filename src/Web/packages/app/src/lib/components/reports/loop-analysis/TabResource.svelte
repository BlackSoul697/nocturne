<!--
  Fetches one tab's response, registered with the report's resource guard, and hands it to the page.
  The page mounts one for the tab on screen only, so a tab not shown is never fetched.
-->
<script lang="ts" generics="T">
  import { untrack } from "svelte";
  import { contextResource } from "$lib/hooks/resource-context.svelte";

  interface Props {
    load: () => { loading: boolean; error: unknown; current: T | undefined; refresh: () => void };
    /** `stale`: `current` answers a superseded request. */
    onupdate: (state: { current: T | undefined; stale: boolean; failed: boolean }) => void;
  }

  let { load, onupdate }: Props = $props();

  const resource = contextResource(() => load(), { errorTitle: "Error Loading Loop Analysis" });

  $effect.pre(() => {
    const state = { current: resource.current, stale: resource.stale, failed: resource.error != null };
    // The page's handler is not a dependency: only the resource's state is.
    untrack(() => onupdate(state));
  });
</script>
