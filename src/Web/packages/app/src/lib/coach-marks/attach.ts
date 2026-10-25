import { page } from "$app/state";
import type { Attachment } from "svelte/attachments";

/** A share viewer or a guest is not toured: the tour's backdrop covers a page they only look at. */
export function toured(): boolean {
  return !!page.data.user && !page.data.isGuestSession;
}

/**
 * Holds `mark` on the element only while the element has a box. A target rendered once per layout,
 * with the other layout hidden, otherwise registers its key twice, and the popover waits on the
 * hidden copy as a step it can never show.
 */
export function whileShown(mark: Attachment<HTMLElement>): Attachment<HTMLElement> {
  return (element) => {
    let release: ReturnType<Attachment<HTMLElement>>;
    let held = false;
    const observer = new ResizeObserver(() => {
      const shown = element.getClientRects().length > 0;
      if (shown === held) return;
      held = shown;
      if (shown) {
        release = mark(element);
      } else {
        release?.();
        release = undefined;
      }
    });
    observer.observe(element);
    return () => {
      observer.disconnect();
      release?.();
    };
  };
}
