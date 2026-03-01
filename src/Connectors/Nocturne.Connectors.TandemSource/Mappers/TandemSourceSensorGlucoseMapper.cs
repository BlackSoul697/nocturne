using Nocturne.Connectors.TandemSource.EventParser;
using Nocturne.Core.Constants;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.TandemSource.Mappers;

/// <summary>
/// Maps CGM reading events (LID_CGM_DATA_GXB, LID_CGM_DATA_G7, LID_CGM_DATA_FSL2)
/// to SensorGlucose records. Uses the EGV timestamp (not event timestamp) for
/// accurate backfill timing.
/// </summary>
public static class TandemSourceSensorGlucoseMapper
{
    public static List<SensorGlucose> Map(List<ParsedEvent> events, TimeZoneInfo timezone)
    {
        if (events.Count == 0) return [];

        var result = new List<SensorGlucose>(events.Count);

        foreach (var evt in events.OrderBy(e => e.TimestampRaw))
        {
            var glucose = evt.GetUInt16("currentGlucoseDisplayValue");
            if (glucose == 0) continue;

            var egvTimestampRaw = evt.GetUInt32("EGV TimeStamp");
            var timestamp = TandemEpoch.ToUtcDateTime(egvTimestampRaw, timezone);

            var now = DateTime.UtcNow;
            result.Add(new SensorGlucose
            {
                Id = Guid.CreateVersion7(),
                Timestamp = timestamp,
                Mgdl = glucose,
                DataSource = DataSources.TandemSourceConnector,
                CreatedAt = now,
                ModifiedAt = now
            });
        }

        return result;
    }
}
