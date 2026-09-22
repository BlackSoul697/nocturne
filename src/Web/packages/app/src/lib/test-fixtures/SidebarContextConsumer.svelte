<script lang="ts">
  import { useSidebar } from "$lib/components/ui/sidebar/context.svelte.js";

  const { marker }: { marker: string } = $props();

  // Dereferenced on purpose: useSidebar() answers undefined rather than
  // throwing, so a provider whose setContext never ran stays invisible until
  // a descendant reads through it — which is how this reached production.
  const sidebar = useSidebar();
</script>

<span data-testid="probe">{marker}:{String(sidebar.isMobile)}</span>
