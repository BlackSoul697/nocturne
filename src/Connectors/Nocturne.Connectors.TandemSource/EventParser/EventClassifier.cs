using Nocturne.Connectors.TandemSource.Models;

namespace Nocturne.Connectors.TandemSource.EventParser;

/// <summary>
/// Routes parsed events to their EventClass category based on event name.
/// Routes parsed events to their processing category based on event name.
/// </summary>
public static class EventClassifier
{
    private static readonly Dictionary<string, EventClass> EventNameToClass = new(StringComparer.OrdinalIgnoreCase)
    {
        // Basal
        ["LID_BASAL_DELIVERY"] = EventClass.Basal,

        // Bolus lifecycle
        ["LID_BOLUS_REQUESTED_MSG1"] = EventClass.Bolus,
        ["LID_BOLUS_REQUESTED_MSG2"] = EventClass.Bolus,
        ["LID_BOLUS_REQUESTED_MSG3"] = EventClass.Bolus,
        ["LID_BOLUS_COMPLETED"] = EventClass.Bolus,
        ["LID_BOLEX_COMPLETED"] = EventClass.Bolus,

        // Suspend / Resume
        ["LID_PUMPING_SUSPENDED"] = EventClass.BasalSuspension,
        ["LID_PUMPING_RESUMED"] = EventClass.BasalResume,

        // Alarms
        ["LID_ALARM_ACTIVATED"] = EventClass.Alarm,
        ["LID_MALFUNCTION_ACTIVATED"] = EventClass.Alarm,

        // Cartridge / site changes
        ["LID_CARTRIDGE_FILLED"] = EventClass.Cartridge,
        ["LID_CANNULA_FILLED"] = EventClass.Cartridge,
        ["LID_TUBING_FILLED"] = EventClass.Cartridge,

        // CGM alerts
        ["LID_CGM_ALERT_ACTIVATED"] = EventClass.CgmAlert,
        ["LID_CGM_ALERT_ACTIVATED_DEX"] = EventClass.CgmAlert,
        ["LID_CGM_ALERT_ACTIVATED_FSL2"] = EventClass.CgmAlert,

        // CGM session lifecycle
        ["LID_CGM_START_SESSION_GX"] = EventClass.CgmStartJoinStop,
        ["LID_CGM_START_SESSION_FSL2"] = EventClass.CgmStartJoinStop,
        ["LID_CGM_JOIN_SESSION_GX"] = EventClass.CgmStartJoinStop,
        ["LID_CGM_JOIN_SESSION_G7"] = EventClass.CgmStartJoinStop,
        ["LID_CGM_JOIN_SESSION_FSL2"] = EventClass.CgmStartJoinStop,
        ["LID_CGM_STOP_SESSION_GX"] = EventClass.CgmStartJoinStop,
        ["LID_CGM_STOP_SESSION_G7"] = EventClass.CgmStartJoinStop,
        ["LID_CGM_STOP_SESSION_FSL2"] = EventClass.CgmStartJoinStop,

        // CGM readings
        ["LID_CGM_DATA_GXB"] = EventClass.CgmReading,
        ["LID_CGM_DATA_G7"] = EventClass.CgmReading,
        ["LID_CGM_DATA_FSL2"] = EventClass.CgmReading,

        // User mode (sleep/exercise)
        ["LID_AA_USER_MODE_CHANGE"] = EventClass.UserMode,

        // Device status
        ["LID_AA_DAILY_STATUS"] = EventClass.DeviceStatus,
    };

    public static EventClass? Classify(ParsedEvent evt) =>
        EventNameToClass.TryGetValue(evt.EventName, out var cls) ? cls : null;

    public static Dictionary<EventClass, List<ParsedEvent>> ClassifyAll(IEnumerable<ParsedEvent> events)
    {
        var result = new Dictionary<EventClass, List<ParsedEvent>>();

        foreach (var evt in events)
        {
            var cls = Classify(evt);
            if (cls == null) continue;

            if (!result.TryGetValue(cls.Value, out var list))
            {
                list = [];
                result[cls.Value] = list;
            }
            list.Add(evt);
        }

        return result;
    }
}
