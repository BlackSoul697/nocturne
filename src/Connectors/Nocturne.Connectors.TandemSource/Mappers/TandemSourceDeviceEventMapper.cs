using Nocturne.Connectors.TandemSource.EventParser;
using Nocturne.Connectors.TandemSource.Models;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.TandemSource.Mappers;

/// <summary>
/// Maps alarm, cartridge, CGM alert, and CGM session events to DeviceEvent records.
/// </summary>
public static class TandemSourceDeviceEventMapper
{
    public static List<DeviceEvent> Map(
        Dictionary<EventClass, List<ParsedEvent>> classified,
        TimeZoneInfo timezone)
    {
        var result = new List<DeviceEvent>();

        if (classified.TryGetValue(EventClass.Alarm, out var alarms))
            result.AddRange(MapAlarms(alarms, timezone));

        if (classified.TryGetValue(EventClass.Cartridge, out var cartridge))
            result.AddRange(MapCartridge(cartridge, timezone));

        if (classified.TryGetValue(EventClass.CgmAlert, out var cgmAlerts))
            result.AddRange(MapCgmAlerts(cgmAlerts, timezone));

        if (classified.TryGetValue(EventClass.CgmStartJoinStop, out var cgmSessions))
            result.AddRange(MapCgmSessions(cgmSessions, timezone));

        return result;
    }

    private static IEnumerable<DeviceEvent> MapAlarms(List<ParsedEvent> events, TimeZoneInfo timezone)
    {
        foreach (var evt in events)
        {
            var notes = evt.EventName switch
            {
                "LID_ALARM_ACTIVATED" => $"Alarm: {evt.GetUInt32("AlarmID")}",
                "LID_MALFUNCTION_ACTIVATED" => $"Malfunction: {evt.GetUInt32("MalfID")}",
                _ => "Pump Alarm"
            };

            var now = DateTime.UtcNow;
            yield return new DeviceEvent
            {
                Id = Guid.CreateVersion7(),
                Timestamp = evt.GetUtcTimestamp(timezone),
                EventType = DeviceEventType.PumpSuspend,
                Notes = notes,
                DataSource = DataSources.TandemSourceConnector,
                SyncIdentifier = evt.SeqNum.ToString(),
                CreatedAt = now,
                ModifiedAt = now
            };
        }
    }

    private static IEnumerable<DeviceEvent> MapCartridge(List<ParsedEvent> events, TimeZoneInfo timezone)
    {
        foreach (var evt in events)
        {
            var (eventType, notes2) = evt.EventName switch
            {
                "LID_CARTRIDGE_FILLED" => (DeviceEventType.ReservoirChange, "Cartridge filled"),
                "LID_CANNULA_FILLED" => (DeviceEventType.SiteChange, "Cannula filled"),
                "LID_TUBING_FILLED" => (DeviceEventType.Priming, "Tubing filled"),
                _ => (DeviceEventType.SiteChange, evt.EventName)
            };

            var now = DateTime.UtcNow;
            yield return new DeviceEvent
            {
                Id = Guid.CreateVersion7(),
                Timestamp = evt.GetUtcTimestamp(timezone),
                EventType = eventType,
                Notes = notes2,
                DataSource = DataSources.TandemSourceConnector,
                SyncIdentifier = evt.SeqNum.ToString(),
                CreatedAt = now,
                ModifiedAt = now
            };
        }
    }

    private static IEnumerable<DeviceEvent> MapCgmAlerts(List<ParsedEvent> events, TimeZoneInfo timezone)
    {
        foreach (var evt in events)
        {
            var now = DateTime.UtcNow;
            yield return new DeviceEvent
            {
                Id = Guid.CreateVersion7(),
                Timestamp = evt.GetUtcTimestamp(timezone),
                EventType = DeviceEventType.SensorChange,
                Notes = $"CGM Alert ({evt.EventName})",
                DataSource = DataSources.TandemSourceConnector,
                SyncIdentifier = evt.SeqNum.ToString(),
                CreatedAt = now,
                ModifiedAt = now
            };
        }
    }

    private static IEnumerable<DeviceEvent> MapCgmSessions(List<ParsedEvent> events, TimeZoneInfo timezone)
    {
        foreach (var evt in events)
        {
            var eventType = evt.EventName.Contains("START") ? DeviceEventType.SensorStart
                : evt.EventName.Contains("JOIN") ? DeviceEventType.SensorStart
                : DeviceEventType.SensorStop;

            var label = evt.EventName.Contains("START") ? "CGM Session Started"
                : evt.EventName.Contains("JOIN") ? "CGM Session Joined"
                : "CGM Session Stopped";

            var now = DateTime.UtcNow;
            yield return new DeviceEvent
            {
                Id = Guid.CreateVersion7(),
                Timestamp = evt.GetUtcTimestamp(timezone),
                EventType = eventType,
                Notes = label,
                DataSource = DataSources.TandemSourceConnector,
                SyncIdentifier = evt.SeqNum.ToString(),
                CreatedAt = now,
                ModifiedAt = now
            };
        }
    }
}
