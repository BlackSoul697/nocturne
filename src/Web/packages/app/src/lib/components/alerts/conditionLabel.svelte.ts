import { AlertConditionType } from "$api-clients";

export function conditionLabel(
  conditionType: AlertConditionType | undefined
): string {
  switch (conditionType) {
    case undefined:
      return "Alert";
    case AlertConditionType.Threshold:
      return "Glucose threshold";
    case AlertConditionType.RateOfChange:
      return "Rate of change";
    case AlertConditionType.SignalLoss:
      return "Signal loss";
    case AlertConditionType.Composite:
      return "Combined conditions";
    case AlertConditionType.Not:
      return "Inverted condition";
    case AlertConditionType.Sustained:
      return "Sustained condition";
    case AlertConditionType.Staleness:
      return "Sensor stale";
    case AlertConditionType.Predicted:
      return "Predicted glucose";
    case AlertConditionType.Trend:
      return "Glucose trend";
    case AlertConditionType.TimeOfDay:
      return "Time of day";
    case AlertConditionType.Iob:
      return "Insulin on board";
    case AlertConditionType.Cob:
      return "Carbs on board";
    case AlertConditionType.Reservoir:
      return "Reservoir";
    case AlertConditionType.SiteAge:
      return "Site age";
    case AlertConditionType.SensorAge:
      return "Sensor age";
    case AlertConditionType.TrackerAge:
      return "Tracker age";
    case AlertConditionType.AlertState:
      return "Other rule state";
    case AlertConditionType.LoopStale:
      return "Loop has stopped";
    case AlertConditionType.LoopEnactionStale:
      return "Loop not enacting";
    case AlertConditionType.PumpSuspended:
      return "Pump suspended";
    case AlertConditionType.PumpBattery:
      return "Pump battery";
    case AlertConditionType.TempBasal:
      return "Temp basal";
    case AlertConditionType.UploaderBattery:
      return "Phone battery";
    case AlertConditionType.OverrideActive:
      return "Override active";
    case AlertConditionType.SensitivityRatio:
      return "Sensitivity";
    case AlertConditionType.DoNotDisturb:
      return "Do not disturb";
    case AlertConditionType.GlucoseBucket:
      return "Glucose bucket";
    case AlertConditionType.TimeSinceLastCarb:
      return "Time since last carbs";
    case AlertConditionType.TimeSinceLastBolus:
      return "Time since last bolus";
    case AlertConditionType.DayOfWeek:
      return "Day of week";
    case AlertConditionType.PumpState:
      return "Pump mode";
    case AlertConditionType.StateSpanActive:
      return "State active";
    case AlertConditionType.SleepSessionActive:
      return "Sleep session";
    default: {
      const unhandled: never = conditionType;
      return unhandled;
    }
  }
}
