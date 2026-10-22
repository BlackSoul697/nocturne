<!--
  Fetches one tab's response into its state for as long as the page shows the tab or keeps it.
  Only the tab on screen reports to the report's resource guard.
-->
<script lang="ts">
  interface Props {
    tab: { mount: (active: () => boolean) => void };
    /** Whether the tab is on screen. */
    active: boolean;
  }

  let { tab, active }: Props = $props();

  // The page keys one per tab, so `tab` never changes under it.
  // svelte-ignore state_referenced_locally
  tab.mount(() => active);
</script>
