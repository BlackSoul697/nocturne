<script lang="ts">
  interface Props {
    layout: "grid" | "list" | "kanban";
    density: "compact" | "standard" | "preview";
    sortMode: "name" | "attention" | "tir";
    onlayout: (v: "grid" | "list" | "kanban") => void;
    ondensity: (v: "compact" | "standard" | "preview") => void;
    onsort: (v: "name" | "attention" | "tir") => void;
  }

  let { layout, density, sortMode, onlayout, ondensity, onsort }: Props = $props();
</script>

<div class="flex flex-wrap items-center gap-2 px-4 py-2 border-b border-border bg-background/50">
  <!-- Sort -->
  <span class="text-xs text-muted-foreground">Sort</span>
  <div class="flex rounded-md border border-border overflow-hidden text-xs">
    {#each [["name","A → Z"],["attention","Attention"],["tir","Lowest TIR"]] as [val, label]}
      <button
        class="px-2.5 py-1 transition-colors {sortMode === val ? 'bg-accent font-medium' : 'hover:bg-accent/50'}"
        onclick={() => onsort(val as "name" | "attention" | "tir")}
      >{label}</button>
    {/each}
  </div>

  <!-- Layout -->
  <span class="text-xs text-muted-foreground ml-2">Layout</span>
  <div class="flex rounded-md border border-border overflow-hidden text-xs">
    {#each [["grid","Grid"],["list","List"],["kanban","By status"]] as [val, label]}
      <button
        class="px-2.5 py-1 transition-colors {layout === val ? 'bg-accent font-medium' : 'hover:bg-accent/50'}"
        onclick={() => onlayout(val as "grid" | "list" | "kanban")}
      >{label}</button>
    {/each}
  </div>

  <!-- Density (grid only) -->
  {#if layout === "grid"}
    <span class="text-xs text-muted-foreground ml-2">Density</span>
    <div class="flex rounded-md border border-border overflow-hidden text-xs">
      {#each [["compact","Compact"],["standard","Standard"],["preview","Preview"]] as [val, label]}
        <button
          class="px-2.5 py-1 transition-colors {density === val ? 'bg-accent font-medium' : 'hover:bg-accent/50'}"
          onclick={() => ondensity(val as "compact" | "standard" | "preview")}
        >{label}</button>
      {/each}
    </div>
  {/if}
</div>
