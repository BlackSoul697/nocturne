type Rounded = "none" | "edge";

/**
 * Rounds only the last series of a stack. layerchart rounds whichever segment is
 * highest in each row, so a row whose last series is zero would otherwise draw a
 * rounded cap on an earlier one.
 */
export function roundLastSeriesOnly<S extends { key: string; props?: object }>(
  series: S[]
): (S & { props: { rounded: Rounded } })[] {
  return series.map((s, i) => ({
    ...s,
    props: { ...s.props, rounded: i === series.length - 1 ? "edge" : "none" },
  }));
}
