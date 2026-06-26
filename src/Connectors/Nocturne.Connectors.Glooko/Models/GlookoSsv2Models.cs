using System.Text.Json.Serialization;

namespace Nocturne.Connectors.Glooko.Models;

/// <summary>
///     Cursor/pagination envelope shared by every SSV2 sync resource. Each resource response carries
///     its records under a resource-specific property (e.g. <c>egvs</c>) plus these three fields, which
///     drive incremental, ordered pagination: feed <see cref="LastUpdatedAt"/> + <see cref="LastGuid"/>
///     back into the next request until <see cref="LastPage"/> is <c>true</c>.
/// </summary>
public abstract class GlookoSsv2Page
{
    [JsonPropertyName("lastPage")] public bool LastPage { get; set; }

    [JsonPropertyName("lastUpdatedAt")] public string? LastUpdatedAt { get; set; }

    [JsonPropertyName("lastGuid")] public string? LastGuid { get; set; }
}

/// <summary>
///     A page of <c>/api/v2/cgm/egvs</c> — the granular per-reading CGM stream the Glooko mobile app
///     consumes (the SSV2 counterpart to the web flow's binned <c>cgm/readings</c>).
/// </summary>
public class GlookoEgvPage : GlookoSsv2Page
{
    [JsonPropertyName("egvs")] public GlookoEgv[]? Egvs { get; set; }
}

// ── Page wrappers reusing the existing v2 record models ─────────────────────
// These SSV2 resources are the same endpoints the windowed v2 path already calls, so their record
// shapes (and mappers) are unchanged; only the page envelope differs.

/// <summary>A page of <c>/api/v2/pumps/normal_boluses</c>.</summary>
public class GlookoNormalBolusPage : GlookoSsv2Page
{
    [JsonPropertyName("normalBoluses")] public GlookoBolus[]? NormalBoluses { get; set; }
}

/// <summary>A page of <c>/api/v2/pumps/scheduled_basals</c>.</summary>
public class GlookoScheduledBasalPage : GlookoSsv2Page
{
    [JsonPropertyName("scheduledBasals")] public GlookoBasal[]? ScheduledBasals { get; set; }
}

/// <summary>A page of <c>/api/v2/pumps/temporary_basals</c>.</summary>
public class GlookoTemporaryBasalPage : GlookoSsv2Page
{
    [JsonPropertyName("temporaryBasals")] public GlookoTempBasal[]? TemporaryBasals { get; set; }
}

/// <summary>A page of <c>/api/v2/pumps/suspend_basals</c>.</summary>
public class GlookoSuspendBasalPage : GlookoSsv2Page
{
    [JsonPropertyName("suspendBasals")] public GlookoSuspendBasal[]? SuspendBasals { get; set; }
}

/// <summary>A page of <c>/api/v2/readings</c> (manual meter readings → BG checks).</summary>
public class GlookoMeterReadingPage : GlookoSsv2Page
{
    [JsonPropertyName("readings")] public GlookoMeterReading[]? Readings { get; set; }
}

/// <summary>A page of <c>/api/v2/foods</c>.</summary>
public class GlookoFoodPage : GlookoSsv2Page
{
    [JsonPropertyName("foods")] public GlookoFood[]? Foods { get; set; }
}

/// <summary>A page of <c>/api/v2/pumps/events</c> (device events: reservoir/site/cannula changes, etc.).</summary>
public class GlookoPumpEventPage : GlookoSsv2Page
{
    [JsonPropertyName("events")] public GlookoPumpEvent[]? Events { get; set; }
}

/// <summary>
///     A single pump device event from the SSV2 <c>pumps/events</c> feed. <see cref="Type"/> is a
///     snake_case event kind (e.g. <c>reservoir_change</c>) mapped to a strongly-typed DeviceEventType.
/// </summary>
public class GlookoPumpEvent
{
    [JsonPropertyName("pumpTimestamp")] public string? PumpTimestamp { get; set; }

    [JsonPropertyName("type")] public string? Type { get; set; }

    [JsonPropertyName("pumpGuid")] public string? PumpGuid { get; set; }

    [JsonPropertyName("guid")] public string? Guid { get; set; }

    [JsonPropertyName("softDeleted")] public bool SoftDeleted { get; set; }
}

/// <summary>
///     A single estimated glucose value from the SSV2 egvs feed.
/// </summary>
public class GlookoEgv
{
    /// <summary>
    ///     Local display wall-clock (fake-UTC, like every other Glooko timestamp). This is the reading's
    ///     clinical time; <see cref="SystemTime"/> is the sensor's own clock and is not timezone-corrected.
    /// </summary>
    [JsonPropertyName("displayTime")] public string? DisplayTime { get; set; }

    [JsonPropertyName("systemTime")] public string? SystemTime { get; set; }

    /// <summary>
    ///     Glucose in mg/dL × 100 (integer encoding for 2-decimal precision), always mg/dL regardless of
    ///     the account's display units — same encoding as the v2 <c>cgm/readings</c> feed.
    /// </summary>
    [JsonPropertyName("glucoseValue")] public double GlucoseValue { get; set; }

    [JsonPropertyName("trendArrow")] public string? TrendArrow { get; set; }

    [JsonPropertyName("guid")] public string? Guid { get; set; }

    [JsonPropertyName("cgmDeviceGuid")] public string? CgmDeviceGuid { get; set; }

    /// <summary>
    ///     True when the value was interpolated/back-filled rather than measured; excluded from ingest,
    ///     matching the v3 graph path which drops calculated readings.
    /// </summary>
    [JsonPropertyName("calculated")] public bool Calculated { get; set; }

    [JsonPropertyName("softDeleted")] public bool SoftDeleted { get; set; }
}

/// <summary>A page of <c>/api/v2/pumps/injection_boluses</c> (manual pen-injected boluses).</summary>
public class GlookoInjectionBolusPage : GlookoSsv2Page
{
    [JsonPropertyName("injectionBoluses")] public GlookoInjectionInsulin[]? InjectionBoluses { get; set; }
}

/// <summary>A page of <c>/api/v2/pumps/injection_basals</c> (manual pen-injected long-acting basal).</summary>
public class GlookoInjectionBasalPage : GlookoSsv2Page
{
    [JsonPropertyName("injectionBasals")] public GlookoInjectionInsulin[]? InjectionBasals { get; set; }
}

/// <summary>
///     A manual pen injection (bolus or basal) from the SSV2 <c>pumps/injection_*</c> feeds. Shares the
///     pump-object envelope (<c>pumpTimestamp</c>/<c>guid</c>/<c>softDeleted</c>) with <see cref="GlookoBolus"/>
///     — both extend the app's GKPumpObject; <see cref="Name"/> is the insulin product name (e.g.
///     "Tresiba®U100"), resolved against the insulin catalog for DIA/peak. The SSV2 counterpart to the
///     v3 graph's <c>gkInsulinBolus</c>/<c>gkInsulinBasal</c> series.
/// </summary>
public class GlookoInjectionInsulin
{
    [JsonPropertyName("pumpTimestamp")] public string PumpTimestamp { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("insulinDelivered")] public double InsulinDelivered { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("guid")] public string? Guid { get; set; }

    [JsonPropertyName("softDeleted")] public bool SoftDeleted { get; set; }
}

/// <summary>A page of <c>/api/v2/pumps/alarms</c> (pump alarms → system events).</summary>
public class GlookoSsv2AlarmPage : GlookoSsv2Page
{
    [JsonPropertyName("alarms")] public GlookoSsv2Alarm[]? Alarms { get; set; }
}

/// <summary>
///     A pump alarm from the SSV2 <c>pumps/alarms</c> feed. Unlike the other (camelCase) pump feeds,
///     these are raw snake_case Mongo documents: <see cref="Value"/> is the alarm code (e.g.
///     "raw_occlusion") and <see cref="AlarmSeverity"/> the severity ("hazard"/"warning"/...). The SSV2
///     counterpart to the v3 graph's <c>pumpAlarm</c> series.
/// </summary>
public class GlookoSsv2Alarm
{
    [JsonPropertyName("pump_timestamp")] public string? PumpTimestamp { get; set; }

    [JsonPropertyName("value")] public string? Value { get; set; }

    [JsonPropertyName("alarm_severity")] public string? AlarmSeverity { get; set; }

    [JsonPropertyName("alarm_type")] public string? AlarmType { get; set; }

    [JsonPropertyName("guid")] public string? Guid { get; set; }

    [JsonPropertyName("soft_deleted")] public bool SoftDeleted { get; set; }
}

/// <summary>A page of <c>/api/v2/cgm/carbs_events</c> (standalone app-logged carbs → CarbIntake).</summary>
public class GlookoCarbsEventPage : GlookoSsv2Page
{
    [JsonPropertyName("carbsEvents")] public GlookoSsv2CarbsEvent[]? CarbsEvents { get; set; }
}

/// <summary>
///     A standalone carb entry logged in the Glooko/CGM app, not attached to a bolus. <see cref="CgmCarbs"/>
///     is the carb amount in grams. The SSV2 counterpart to the v3 graph's <c>carbAll</c> series.
/// </summary>
public class GlookoSsv2CarbsEvent
{
    [JsonPropertyName("timestamp")] public string? Timestamp { get; set; }

    [JsonPropertyName("eventTime")] public string? EventTime { get; set; }

    [JsonPropertyName("cgmCarbs")] public double CgmCarbs { get; set; }

    [JsonPropertyName("guid")] public string? Guid { get; set; }

    [JsonPropertyName("softDeleted")] public bool SoftDeleted { get; set; }
}
