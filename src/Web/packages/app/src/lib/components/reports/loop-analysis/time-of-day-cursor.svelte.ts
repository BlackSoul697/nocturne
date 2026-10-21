/** The slot a stack of time-of-day panels hovers and the one picked for the drill-down, shared across the stack. */
export class TimeOfDayCursor {
  slot = $state<number | null>(null);
  /** The panel under the pointer, which alone shows the tooltip; the others show only the crosshair. */
  panel = $state<string | null>(null);
  selected = $state<number | null>(null);

  hover(panel: string, slot: number) {
    this.panel = panel;
    this.slot = slot;
  }

  leave(panel: string) {
    if (this.panel !== panel) return;
    this.panel = null;
    this.slot = null;
  }

  select(slot: number) {
    this.selected = this.selected === slot ? null : slot;
  }
}
