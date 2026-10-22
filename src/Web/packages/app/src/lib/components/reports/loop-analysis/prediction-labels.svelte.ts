import { PredictionContext, PredictionCurve } from "$lib/api";
import { plural } from "$lib/utils/plural";
import { formatLocale } from "$lib/utils/formatting";

/** Built per call so wuchale reads them in the current locale. */
export function curveLabel(curve: PredictionCurve | undefined): string {
  switch (curve) {
    case PredictionCurve.Iob:
      return "IOB";
    case PredictionCurve.ZeroTemp:
      return "Zero temp";
    case PredictionCurve.Cob:
      return "COB";
    case PredictionCurve.Uam:
      return "UAM";
    default:
      return "Prediction";
  }
}

/** What each curve assumes, as a one-line caption under its chart. */
export function curveCaption(curve: PredictionCurve | undefined): string {
  switch (curve) {
    case PredictionCurve.Iob:
      return "Insulin on board alone, no carbs";
    case PredictionCurve.ZeroTemp:
      return "Insulin on board with basal stopped from the prediction on";
    case PredictionCurve.Cob:
      return "Announced carbs absorbing";
    case PredictionCurve.Uam:
      return "The rise it sees continuing, announced or not";
    default:
      return "The single curve the loop acts on";
  }
}

export function contextLabel(context: PredictionContext | undefined): string {
  switch (context) {
    case PredictionContext.Adjusted:
      return "Override, temp target or exercise";
    case PredictionContext.PostMeal:
      return "After a meal";
    case PredictionContext.PostCorrection:
      return "After a correction";
    default:
      return "Fasting";
  }
}

export function contextCaption(context: PredictionContext | undefined): string {
  switch (context) {
    case PredictionContext.Adjusted:
      return "An override, temporary target or exercise was running";
    case PredictionContext.PostMeal:
      return "Carbs entered in the 3 hours before";
    case PredictionContext.PostCorrection:
      return "A manual bolus in the 3 hours before, no carbs";
    default:
      return "None of the above";
  }
}

export function horizonLabel(minutes: number | undefined): string {
  return plural(minutes ?? 0, ["+# min", "+# min"]);
}

/** The served settings-signal horizons as a list, e.g. "+30 min and +60 min". */
export function horizonList(minutes: number[]): string {
  return new Intl.ListFormat(formatLocale(), { type: "conjunction" }).format(minutes.map(horizonLabel));
}
