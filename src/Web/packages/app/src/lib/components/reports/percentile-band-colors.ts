/** Percentile band colours shared by every chart of a series, so one series reads the same everywhere. */
export interface BandColors {
  outer: string;
  inner: string;
  median: string;
}

export const GLUCOSE_BANDS: BandColors = {
  outer: "oklch(from var(--chart-1) l c h / 0.35)",
  inner: "oklch(from var(--chart-1) l c h / 0.6)",
  median: "var(--chart-1)",
};

/** Tints of the one basal hue; texture tells them apart in print. */
export const BASAL_BANDS: BandColors = {
  outer: "color-mix(in oklab, var(--basal) 45%, var(--background))",
  inner: "var(--basal)",
  median: "var(--entry-basal)",
};
