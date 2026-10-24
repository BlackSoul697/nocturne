import { describe, it, expect } from "vitest";
import { AlertConditionType } from "$api-clients";
import { conditionLabel } from "./conditionLabel.svelte";

describe("conditionLabel", () => {
  it.each([
    [AlertConditionType.Threshold, "Glucose threshold"],
    [AlertConditionType.RateOfChange, "Rate of change"],
    [AlertConditionType.SignalLoss, "Signal loss"],
    [AlertConditionType.Composite, "Combined conditions"],
    [AlertConditionType.PumpBattery, "Pump battery"],
    [undefined, "Alert"],
  ])("labels %s as %s", (type, label) => {
    expect(conditionLabel(type)).toBe(label);
  });

  it("never shows a raw condition type", () => {
    for (const type of Object.values(AlertConditionType)) {
      expect(conditionLabel(type)).not.toBe(type);
    }
  });
});
