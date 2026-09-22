<script lang="ts">
  import * as Sidebar from "$lib/components/ui/sidebar";
  import SidebarContextConsumer from "./SidebarContextConsumer.svelte";

  const { marker }: { marker: string } = $props();
</script>

<Sidebar.Provider>
  <SidebarContextConsumer {marker} />
</Sidebar.Provider>
