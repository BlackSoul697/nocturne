import { render } from "vitest-browser-svelte";
import { page } from "vitest/browser";
import { afterEach, describe, expect, it } from "vitest";
import { TimeZoneUnavailableReason } from "$lib/api";
import { glucoseUnits } from "$lib/stores/appearance-store.svelte";
import SettingsEvidenceTab from "./SettingsEvidenceTab.svelte";
import {
  settingsEvidenceRange,
  settingsEvidenceResponse,
  WITHHELD_SETTINGS_EVIDENCE,
} from "./settings-evidence.test-fixtures";

/** A block's disclosure, by the hour span its summary begins with. */
const block = (section: string, span: RegExp) =>
  page.getByRole("list", { name: `${section} by block` }).getByRole("listitem").filter({ hasText: span });

describe("SettingsEvidenceTab", () => {
  afterEach(() => {
    glucoseUnits.current = "mg/dl";
  });

  it("states that these are observations, not advice, above every chart", async () => {
    render(SettingsEvidenceTab, { props: { response: settingsEvidenceResponse() } });

    await expect.element(page.getByText("Observations, not advice")).toBeVisible();
    await expect.element(page.getByText(/Talk any change to your settings through with your diabetes care team/)).toBeVisible();
    for (const chart of ["Basal by time of day", "Insulin sensitivity (ISF) by time of day", "Carb ratio by time of day"])
      await expect.element(page.getByRole("slider", { name: chart })).toBeVisible();
  });

  it("shows each block's observed value against the profile's, with n and confidence", async () => {
    render(SettingsEvidenceTab, { props: { response: settingsEvidenceResponse() } });

    const night = block("Basal", /^\s*2/);
    await expect.element(night.first()).toHaveTextContent(/1\.05\s*U\/h, profile 0\.80/);
    await expect.element(night.first()).toHaveTextContent("n = 14");
    await expect.element(night.first()).toHaveTextContent("Moderate confidence");
    await expect.element(block("Basal", /^\s*4/).first()).toHaveTextContent("Low confidence");
    await expect.element(block("Carb ratio", /^\s*6/).first()).toHaveTextContent(/12\.5\s*g\/U, profile 10\.0/);
    await expect.element(block("Carb ratio", /^\s*6/).first()).toHaveTextContent("High confidence");
  });

  it("says in plain words why a block has no figure, a missing meal in this block", async () => {
    render(SettingsEvidenceTab, { props: { response: settingsEvidenceResponse() } });

    await expect.element(block("Carb ratio", /^\s*5\s*PM|^\s*17/).first()).toHaveTextContent("No meals were logged with carbs in this block");
    await expect.element(block("Insulin sensitivity (ISF)", /^\s*12\s*AM|^\s*0/).first()).toHaveTextContent("No correction boluses in this block");
    await expect.element(block("Insulin sensitivity (ISF)", /^\s*5\s*PM|^\s*17/).first()).toHaveTextContent("Too few episodes to show a figure");
    await expect.element(block("Basal", /^\s*10/).first()).toHaveTextContent("Every candidate was set aside");
  });

  it("opens a block to say why its confidence is not higher, what would raise it, and what was set aside", async () => {
    render(SettingsEvidenceTab, { props: { response: settingsEvidenceResponse() } });

    const night = block("Basal", /^\s*4/).first();
    await night.getByText(/1\.05/).click();

    await expect.element(night.getByText("Why the confidence is not higher")).toBeVisible();
    await expect.element(night.getByText(/Carbs are logged on few days/)).toBeVisible();
    await expect.element(night.getByText("Logging carbs would let us tell fasting time from meals.")).toBeVisible();
    await expect.element(night.getByText("Carbs within 4 hours before, or during")).toBeVisible();
    await expect.element(night.getByText("The loop's mean correction: +0.25 U/h")).toBeVisible();
    await expect.element(night.getByText("In the same direction on 93% of days")).toBeVisible();
  });

  it("notes beside a figure towards more insulin that it is an observation to discuss, and not beside one that is not", async () => {
    render(SettingsEvidenceTab, { props: { response: settingsEvidenceResponse() } });

    const isf = block("Insulin sensitivity (ISF)", /^\s*11/).first();
    await isf.getByText(/41/).first().click();
    await expect.element(isf.getByText(/This would mean more insulin than your profile gives/)).toBeVisible();

    const cr = block("Carb ratio", /^\s*6/).first();
    await cr.getByText(/12\.5/).first().click();
    await expect.element(cr.getByText(/This would mean more insulin/)).not.toBeInTheDocument();
  });

  it("lists a block's episodes with what acted, each opening Day in Review", async () => {
    render(SettingsEvidenceTab, { props: { response: settingsEvidenceResponse() } });

    const isf = block("Insulin sensitivity (ISF)", /^\s*11/).first();
    await isf.getByText(/41/).first().click();

    for (const heading of ["Net insulin, U", "IOB start, U", "IOB end, U", "Residual IOB, U", "Basal excess, U", "Start", "End"])
      await expect.element(isf.getByRole("columnheader", { name: heading, exact: true })).toBeVisible();
    const link = isf.getByRole("link");
    await expect.element(link).toHaveAttribute("href", "/reports/day-in-review?date=2026-06-12&time=14:00");
    await expect.element(link).toHaveAccessibleName(/in Day in Review/);

    const night = block("Basal", /^\s*2/).first();
    await night.getByText(/1\.05/).click();
    await expect.element(night.getByRole("columnheader", { name: "Carried insulin, U/h" })).toBeVisible();
  });

  it("shows ISF in the viewer's units", async () => {
    glucoseUnits.current = "mmol";
    render(SettingsEvidenceTab, { props: { response: settingsEvidenceResponse() } });

    await expect.element(block("Insulin sensitivity (ISF)", /^\s*11/).first()).toHaveTextContent(/2\.3\s*mmol\/L per U, profile 2\.8/);
  });

  it("adds the compared range beside each figure", async () => {
    render(SettingsEvidenceTab, {
      props: { response: settingsEvidenceResponse({ comparison: settingsEvidenceRange({ shift: 0.2, meals: false }) }) },
    });

    await expect.element(block("Basal", /^\s*2/).first()).toHaveTextContent("compared 1.25");
    await expect.element(block("Carb ratio", /^\s*6/).first()).toHaveTextContent("compared –");
    await expect.element(page.getByText("Compared range").first()).toBeVisible();
  });

  it("says the tab is withheld rather than showing empty evidence", async () => {
    render(SettingsEvidenceTab, { props: { response: settingsEvidenceResponse(WITHHELD_SETTINGS_EVIDENCE) } });

    await expect.element(page.getByText("Your access does not include the data this tab is drawn from")).toBeVisible();
    await expect.element(page.getByRole("slider")).not.toBeInTheDocument();
  });

  it("explains a missing time zone and a history clamp", async () => {
    render(SettingsEvidenceTab, {
      props: { response: settingsEvidenceResponse({ timeZoneUnavailableReason: TimeZoneUnavailableReason.NotConfigured, primary: undefined }) },
    });
    await expect.element(page.getByText("This report needs a time zone")).toBeVisible();
  });

  it("says only the last 24 hours are included for a clamped viewer", async () => {
    render(SettingsEvidenceTab, { props: { response: settingsEvidenceResponse({ historyClamped: true }) } });
    await expect.element(page.getByText("Only the last 24 hours are included")).toBeVisible();
  });

  it("names a filter the viewer's access ignored", async () => {
    render(SettingsEvidenceTab, { props: { response: settingsEvidenceResponse({ ignoredFilters: ["excludeOverrides"] }) } });
    await expect.element(page.getByText("Some filters were not applied")).toBeVisible();
  });
});
