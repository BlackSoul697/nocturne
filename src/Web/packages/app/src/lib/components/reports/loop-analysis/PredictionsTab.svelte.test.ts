import { render } from "vitest-browser-svelte";
import { page, userEvent } from "vitest/browser";
import { describe, expect, it } from "vitest";
import { PredictionCurve, TimeZoneUnavailableReason } from "$lib/api";
import PredictionsTab from "./PredictionsTab.svelte";
import { predictionResponse } from "./prediction-accuracy.test-fixtures";

const NO_DEVICES = ["glucose.read", "treatments.read", "therapy.read", "reports.read"];
const NO_TREATMENTS = ["glucose.read", "devices.read", "therapy.read", "reports.read"];

async function focusHorizon(curve: string, steps: number) {
  const slider = page.getByRole("slider", { name: curve, exact: true });
  (slider.element() as HTMLElement).focus();
  for (let i = 0; i < steps; i++) await userEvent.keyboard("{ArrowRight}");
  return page.getByRole("tooltip");
}

describe("PredictionsTab", () => {
  it("draws each curve's error by horizon with the figures and the framing", async () => {
    render(PredictionsTab, { props: { response: predictionResponse() } });

    for (const curve of ["IOB", "Zero temp", "COB", "UAM"])
      await expect.element(page.getByRole("slider", { name: curve, exact: true })).toBeVisible();
    await expect.element(page.getByText("4,032")).toBeVisible();
    await expect.element(page.getByText(/Only the bias at \+30 and \+60 minutes is a sign of your/)).toBeVisible();
    await expect.element(page.getByText(/oref emits its UAM curve only while/)).toBeVisible();
    await expect.element(page.getByText(/oref cuts its zero-temp curve/)).toBeVisible();
    await expect.element(page.getByText(/counted as interrupted and left out of every bias/)).toBeVisible();
  });

  it("reads a horizon's bias, MAE and n as the slider's value and in its tooltip", async () => {
    render(PredictionsTab, { props: { response: predictionResponse() } });

    const tooltip = await focusHorizon("IOB", 1);
    await expect
      .element(page.getByRole("slider", { name: "IOB", exact: true }))
      .toHaveAttribute("aria-valuetext", "+60 min: bias -8 mg/dL, MAE 20 mg/dL, 400 predictions");
    await expect.element(tooltip.getByText("IOB, +60 min")).toBeVisible();
    await expect.element(tooltip.getByText(/settings signal/)).toBeVisible();

    await userEvent.keyboard("{End}");
    await expect.element(page.getByRole("tooltip").getByText("IOB, +120 min")).toBeVisible();
    expect(page.getByRole("tooltip").getByText(/settings signal/).elements()).toHaveLength(0);
  });

  it("tints the hourly bias and lists each context for the curve picked", async () => {
    render(PredictionsTab, { props: { response: predictionResponse() } });

    await expect.element(page.getByText("IOB bias by hour of day", { exact: true })).toBeVisible();
    await expect.element(page.getByText("After a correction")).toBeVisible();
    await expect.element(page.getByText("Fasting", { exact: true })).toBeVisible();

    await page.getByRole("radio", { name: "UAM" }).click();
    await expect.element(page.getByText("UAM by context", { exact: true })).toBeVisible();
    await expect.element(page.getByText("Override, temp target or exercise").first()).toBeVisible();
    await expect.element(page.getByText(/low "eating soon" temporary targets/)).toBeVisible();
  });

  it("fades sparse horizons and says so", async () => {
    render(PredictionsTab, { props: { response: predictionResponse({ sparse: true }) } });

    await expect.element(page.getByText(/Faded horizons rest on too few days/)).toBeVisible();
    const tooltip = await focusHorizon("UAM", 0);
    await expect.element(tooltip.getByText("Too few days to read as a pattern")).toBeVisible();
  });

  it("outlines the comparison and reads its bias beside the range's", async () => {
    render(PredictionsTab, { props: { response: predictionResponse({ comparison: true }) } });

    await expect.element(page.getByText(/comparison range is drawn as dashed outlines/)).toBeVisible();
    await expect.element(page.getByText("4,032 in the compared range")).toBeVisible();
    const tooltip = await focusHorizon("COB", 0);
    await expect.element(tooltip.getByText("Compared bias")).toBeVisible();
    await expect.element(tooltip.getByText("+2 mg/dL")).toBeVisible();
  });

  it("hides every curve when the caller cannot read the device data they come from", async () => {
    render(PredictionsTab, { props: { response: predictionResponse({ scopes: NO_DEVICES }) } });

    await expect.element(page.getByText("Prediction accuracy is hidden")).toBeVisible();
    await expect.element(page.getByText(/your access does not include\s+device data/)).toBeVisible();
    expect(page.getByRole("slider").elements()).toHaveLength(0);
  });

  it("without treatments keeps the curves read in every context, and no context or interruption", async () => {
    render(PredictionsTab, { props: { response: predictionResponse({ scopes: NO_TREATMENTS, interrupted: 7 }) } });

    await expect.element(page.getByRole("slider", { name: "UAM", exact: true })).toBeVisible();
    expect(page.getByRole("slider", { name: "IOB", exact: true }).elements()).toHaveLength(0);
    await expect.element(page.getByText(/your access does not include treatments/)).toBeVisible();
    expect(page.getByText("After a correction").elements()).toHaveLength(0);

    const tooltip = await focusHorizon("UAM", 0);
    await expect.element(tooltip.getByText("Bias")).toBeVisible();
    expect(tooltip.getByText("Interrupted").elements()).toHaveLength(0);
  });

  it("counts interrupted horizons in the context table and the tooltip", async () => {
    render(PredictionsTab, {
      props: { response: predictionResponse({ curves: [PredictionCurve.Default], interrupted: 7 }) },
    });

    await expect.element(page.getByText("Unmatched 5, excluded 2, interrupted 7").first()).toBeVisible();
    await expect.element(page.getByText(/Interrupted counts are left out of the bias/)).toBeVisible();
    const tooltip = await focusHorizon("Prediction", 0);
    await expect.element(tooltip.getByText("Interrupted")).toBeVisible();
  });

  it("explains a missing time zone in place of the figures", async () => {
    render(PredictionsTab, {
      props: {
        response: {
          ...predictionResponse(),
          primary: { curves: [] },
          timeZoneUnavailableReason: TimeZoneUnavailableReason.NotConfigured,
        },
      },
    });

    await expect.element(page.getByText("This report needs a time zone")).toBeVisible();
    expect(page.getByText("Predictions analysed").elements()).toHaveLength(0);
  });

  it("says a clamped viewer sees only the last 24 hours", async () => {
    render(PredictionsTab, { props: { response: predictionResponse({ historyClamped: true }) } });

    await expect.element(page.getByText("Only the last 24 hours are included")).toBeVisible();
    await expect.element(page.getByText(/left out until their readings arrive/)).toBeVisible();
  });

  it("says when the range has no predictions", async () => {
    render(PredictionsTab, { props: { response: predictionResponse({ curves: [] }) } });

    await expect.element(page.getByText("No predictions in this range")).toBeVisible();
  });
});
