using Nocturne.Connectors.TandemSource.EventParser;
using Nocturne.Core.Constants;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.TandemSource.Mappers;

/// <summary>
/// Maps LID_BASAL_DELIVERY events to TempBasal records.
/// Basal events arrive every ~5 minutes; duration is the gap to the next event.
/// commandedRate is in milliunits/hr (divide by 1000).
/// </summary>
public static class TandemSourceBasalMapper
{
    private static readonly string[] RateSources =
        ["Suspended", "Profile", "Temp Rate", "Algorithm", "Temp Rate and Algorithm"];

    public static List<TempBasal> Map(List<ParsedEvent> events, TimeZoneInfo timezone, DateTime? syncWindowEnd = null)
    {
        if (events.Count == 0) return [];

        var sorted = events.OrderBy(e => e.TimestampRaw).ToList();
        var result = new List<TempBasal>(sorted.Count);

        for (var i = 0; i < sorted.Count; i++)
        {
            var evt = sorted[i];
            var startTime = evt.GetUtcTimestamp(timezone);

            DateTime endTime;
            if (i < sorted.Count - 1)
                endTime = sorted[i + 1].GetUtcTimestamp(timezone);
            else
                endTime = syncWindowEnd ?? startTime.AddMinutes(5);

            var commandedRate = evt.GetUInt16("Commanded Rate");
            var rate = Math.Round(commandedRate / 1000.0, 3);

            if (rate < 0.001 && endTime == startTime)
                continue;

            var profileRate = evt.GetUInt16("Profile Basal Rate");
            var scheduledRate = Math.Round(profileRate / 1000.0, 3);

            var rateSourceRaw = evt.GetUInt16("Commanded Rate Source");
            var origin = rateSourceRaw switch
            {
                0 => TempBasalOrigin.Suspended,
                1 => TempBasalOrigin.Scheduled,
                2 => TempBasalOrigin.Manual,
                3 => TempBasalOrigin.Algorithm,
                4 => TempBasalOrigin.Algorithm,
                _ => TempBasalOrigin.Inferred
            };

            var now = DateTime.UtcNow;
            result.Add(new TempBasal
            {
                Id = Guid.CreateVersion7(),
                StartTimestamp = startTime,
                EndTimestamp = endTime,
                Rate = rate,
                ScheduledRate = scheduledRate,
                Origin = origin,
                DataSource = DataSources.TandemSourceConnector,
                PumpRecordId = evt.SeqNum.ToString(),
                CreatedAt = now,
                ModifiedAt = now
            });
        }

        return result;
    }
}
