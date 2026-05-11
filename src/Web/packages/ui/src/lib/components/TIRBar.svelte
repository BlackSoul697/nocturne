<script lang="ts">
  interface TIR {
    veryLow: number;
    low: number;
    inRange: number;
    high: number;
    veryHigh: number;
  }

  interface Props {
    tir: TIR;
  }

  let { tir }: Props = $props();
</script>

<div class="flex h-1.5 w-full overflow-hidden rounded-full">
  <span style="width:{tir.veryLow}%" class="bg-[var(--glucose-very-low)]"></span>
  <span style="width:{tir.low}%" class="bg-[var(--glucose-low)]"></span>
  <span style="width:{tir.inRange}%" class="bg-[var(--glucose-in-range)]"></span>
  <span style="width:{tir.high}%" class="bg-[var(--glucose-high)]"></span>
  <span style="width:{tir.veryHigh}%" class="bg-[var(--glucose-very-high)]"></span>
</div>
