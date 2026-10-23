using Nocturne.Core.Models.Alerts;

namespace Nocturne.Alerts.ParityCorpus.Generator.Harness;

/// <summary>
/// The condition kinds whose truth can change while every fact except the clock stands still
/// (<c>docs/alerts/engine-semantics.md</c> §5.1), written into the enum manifest the Rust crate
/// checks its own list against. The engine decides which rules the sweep evaluates.
/// </summary>
public static class WallClockKinds
{
    /// <summary>
    /// Kinds that measure elapsed time against an anchor a reading does not move. The anchors are
    /// the newest reading, a loop cycle, a device or tracker start, a treatment, another alert's
    /// trigger or acknowledgement, and the start of a suspension, override, DND or state span.
    /// </summary>
    public static IReadOnlySet<AlertConditionType> Kinds { get; } = new HashSet<AlertConditionType>
    {
        AlertConditionType.SignalLoss,
        AlertConditionType.Staleness,
        AlertConditionType.LoopStale,
        AlertConditionType.LoopEnactionStale,
        AlertConditionType.SiteAge,
        AlertConditionType.SensorAge,
        AlertConditionType.TrackerAge,
        AlertConditionType.TimeSinceLastCarb,
        AlertConditionType.TimeSinceLastBolus,
        AlertConditionType.AlertState,
        AlertConditionType.PumpSuspended,
        AlertConditionType.OverrideActive,
        AlertConditionType.DoNotDisturb,
        AlertConditionType.PumpState,
        AlertConditionType.StateSpanActive,
    };

    /// <summary>
    /// Every other kind, listed so a new <see cref="AlertConditionType"/> member has to be placed
    /// on one side. Containers are here since a <c>sustained</c> timer over a reading-driven
    /// child would only extrapolate the last reading. <c>time_of_day</c> and <c>day_of_week</c>
    /// gate reading-driven leaves, so the next reading bounds their delay.
    /// </summary>
    public static IReadOnlySet<AlertConditionType> NotWallClock { get; } = new HashSet<AlertConditionType>
    {
        AlertConditionType.Threshold,
        AlertConditionType.RateOfChange,
        AlertConditionType.Composite,
        AlertConditionType.Not,
        AlertConditionType.Sustained,
        AlertConditionType.Predicted,
        AlertConditionType.Trend,
        AlertConditionType.TimeOfDay,
        AlertConditionType.Iob,
        AlertConditionType.Cob,
        AlertConditionType.Reservoir,
        AlertConditionType.PumpBattery,
        AlertConditionType.TempBasal,
        AlertConditionType.UploaderBattery,
        AlertConditionType.SensitivityRatio,
        AlertConditionType.GlucoseBucket,
        AlertConditionType.DayOfWeek,
        AlertConditionType.SleepSessionActive,
    };
}
