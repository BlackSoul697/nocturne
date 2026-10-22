<script lang="ts">
  import type { Snippet } from "svelte";

  let {
    children,
    class: className = "",
  }: {
    children: Snippet;
    class?: string;
  } = $props();

  let sentinel = $state<HTMLDivElement>();
  let panel = $state<HTMLDivElement>();
  /** Pixels the stuck panel is slid up out of view. */
  let hidden = $state(0);
  let snapping = $state(false);
  let lastScrollY = 0;
  let scrollingDown = false;

  /**
   * How far the page has scrolled past the panel's place in the flow. The panel is never slid
   * further than this, so it comes to rest in the flow as the page returns to the top.
   */
  function travelled() {
    if (!sentinel || !panel) return 0;
    const top = parseFloat(getComputedStyle(panel).top) || 0;
    return Math.max(0, top - sentinel.getBoundingClientRect().top);
  }

  function onscroll() {
    const delta = window.scrollY - lastScrollY;
    lastScrollY = window.scrollY;
    if (delta !== 0) scrollingDown = delta > 0;
    snapping = false;
    const height = panel?.offsetHeight ?? 0;
    hidden = Math.min(travelled(), Math.max(0, Math.min(height, hidden + delta)));
  }

  /** A panel left partly revealed when scrolling stops finishes the way the page was scrolling. */
  function onscrollend() {
    const height = panel?.offsetHeight ?? 0;
    if (hidden <= 0 || hidden >= height) return;
    snapping = true;
    hidden = scrollingDown ? Math.min(height, travelled()) : 0;
  }
</script>

<svelte:window {onscroll} {onscrollend} />

<div bind:this={sentinel} aria-hidden="true"></div>
<div
  bind:this={panel}
  class={[
    "sticky top-[calc(var(--app-sticky-top,0px)+3.5rem)] z-10 translate-y-(--headroom-offset) print:static print:translate-y-0",
    snapping && "transition-transform duration-200 ease-out motion-reduce:transition-none",
    className,
  ]}
  style:--headroom-offset="{-hidden}px"
  onfocusin={() => {
    snapping = true;
    hidden = 0;
  }}
>
  {@render children()}
</div>
