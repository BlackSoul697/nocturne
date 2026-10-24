import { describe, it, expect } from "vitest";
import { roundLastSeriesOnly } from "./stack-rounding";

describe("roundLastSeriesOnly", () => {
  it("squares every series but the last and keeps existing props", () => {
    const result = roundLastSeriesOnly([
      { key: "a", props: { class: "x" } },
      { key: "b" },
      { key: "c" },
    ]);

    expect(result.map((s) => s.props.rounded)).toEqual(["none", "none", "edge"]);
    expect(result[0].props).toMatchObject({ class: "x" });
  });
});
