import { afterEach, describe, expect, it, vi } from "vitest";
import { whileShown } from "./attach.svelte";

vi.mock("$app/state", () => ({ page: { data: {} } }));

const frame = () => new Promise((resolve) => requestAnimationFrame(() => setTimeout(resolve, 0)));

function target(display: string) {
  const element = document.createElement("div");
  element.style.display = display;
  element.textContent = "Target";
  document.body.append(element);
  return element;
}

describe("whileShown", () => {
  afterEach(() => document.body.replaceChildren());

  it("holds the mark while the element has a box, and releases it when the element is hidden", async () => {
    const release = vi.fn();
    const mark = vi.fn(() => release);
    const element = target("block");

    const detach = whileShown(mark)(element);
    await frame();
    expect(mark).toHaveBeenCalledOnce();

    element.style.display = "none";
    await frame();
    expect(release).toHaveBeenCalledOnce();

    element.style.display = "block";
    await frame();
    expect(mark).toHaveBeenCalledTimes(2);

    if (typeof detach === "function") detach();
    expect(release).toHaveBeenCalledTimes(2);
  });

  it("never holds the mark on an element that stays hidden", async () => {
    const mark = vi.fn();
    const detach = whileShown(mark)(target("none"));
    await frame();

    expect(mark).not.toHaveBeenCalled();
    if (typeof detach === "function") detach();
  });
});
