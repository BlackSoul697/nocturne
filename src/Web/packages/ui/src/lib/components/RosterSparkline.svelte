<script lang="ts">
  interface Props {
    /** Up to 36 glucose readings, oldest-first */
    points: number[];
    /** Status colour token e.g. "var(--glucose-low)" */
    color: string;
    width?: number;
    height?: number;
    /** Show the 70–180 in-range band */
    band?: boolean;
  }

  let { points, color, width = 220, height = 38, band = true }: Props = $props();

  const MIN_Y = 40;
  const MAX_Y = 320;

  function toSvgY(v: number): number {
    return height - ((Math.max(MIN_Y, Math.min(MAX_Y, v)) - MIN_Y) / (MAX_Y - MIN_Y)) * height;
  }

  const path = $derived(
    points.length
      ? points
          .map((v, i) => {
            const x = ((i / (points.length - 1)) * width).toFixed(1);
            const y = toSvgY(v).toFixed(1);
            return `${i === 0 ? "M" : "L"}${x},${y}`;
          })
          .join(" ")
      : ""
  );

  const bandYHi = $derived(toSvgY(180));
  const bandYLo = $derived(toSvgY(70));
  const lastX = $derived(width);
  const lastY = $derived(points.length ? toSvgY(points[points.length - 1]) : 0);
</script>

{#if points.length > 1}
  <svg viewBox="0 0 {width} {height}" preserveAspectRatio="none" class="w-full h-full overflow-visible">
    {#if band}
      <rect
        x="0" y={bandYHi}
        {width}
        height={Math.max(0, bandYLo - bandYHi)}
        fill="var(--glucose-in-range)"
        opacity="0.10"
      />
    {/if}
    <path
      d={path}
      fill="none"
      stroke={color}
      stroke-width="1.6"
      stroke-linecap="round"
      stroke-linejoin="round"
    />
    <circle cx={lastX} cy={lastY} r="2.2" fill={color} />
  </svg>
{/if}
