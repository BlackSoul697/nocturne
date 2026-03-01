using Nocturne.Connectors.TandemSource.EventParser;
using Nocturne.Core.Constants;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.TandemSource.Mappers;

/// <summary>
/// Maps bolus event groups to Bolus records.
/// Boluses span multiple events sharing the same BolusID:
///   - LID_BOLUS_REQUESTED_MSG1: carbs, BG, IOB
///   - LID_BOLUS_REQUESTED_MSG2: options, overrides
///   - LID_BOLUS_REQUESTED_MSG3: total requested
///   - LID_BOLUS_COMPLETED: actual insulin delivered (triggers record creation)
///   - LID_BOLEX_COMPLETED: extended bolus completion
/// </summary>
public static class TandemSourceBolusMapper
{
    public static List<Bolus> Map(List<ParsedEvent> events, TimeZoneInfo timezone)
    {
        if (events.Count == 0) return [];

        var sorted = events.OrderBy(e => e.TimestampRaw).ToList();

        var eventsByBolusId = new Dictionary<ushort, BolusEventGroup>();
        var completedBoluses = new List<(ParsedEvent completed, ushort bolusId)>();

        foreach (var evt in sorted)
        {
            var bolusId = evt.GetUInt16("BolusID");

            if (!eventsByBolusId.TryGetValue(bolusId, out var group))
            {
                group = new BolusEventGroup();
                eventsByBolusId[bolusId] = group;
            }

            switch (evt.EventName)
            {
                case "LID_BOLUS_REQUESTED_MSG1":
                    group.Msg1 = evt;
                    break;
                case "LID_BOLUS_REQUESTED_MSG2":
                    group.Msg2 = evt;
                    break;
                case "LID_BOLUS_REQUESTED_MSG3":
                    group.Msg3 = evt;
                    break;
                case "LID_BOLUS_COMPLETED":
                    group.Completed = evt;
                    completedBoluses.Add((evt, bolusId));
                    break;
                case "LID_BOLEX_COMPLETED":
                    group.BolexCompleted = evt;
                    break;
            }
        }

        var result = new List<Bolus>(completedBoluses.Count);

        foreach (var (completedEvt, bolusId) in completedBoluses.OrderBy(x => x.completed.TimestampRaw))
        {
            var group = eventsByBolusId[bolusId];
            var bolus = MapBolusGroup(group, timezone);
            if (bolus != null)
                result.Add(bolus);
        }

        return result;
    }

    private static Bolus? MapBolusGroup(BolusEventGroup group, TimeZoneInfo timezone)
    {
        if (group.Completed == null) return null;

        var delivered = Math.Round(group.Completed.GetFloat32("InsulinDelivered"), 2);
        var requested = Math.Round(group.Completed.GetFloat32("InsulinRequested"), 2);
        var timestamp = group.Completed.GetUtcTimestamp(timezone);

        var bolusType = BolusType.Normal;
        var automatic = false;
        var kind = BolusKind.Manual;
        double? duration = null;

        if (group.Msg2 != null)
        {
            var optionsRaw = group.Msg2.GetUInt8("Options");
            (bolusType, automatic, kind) = optionsRaw switch
            {
                3 => (BolusType.Normal, true, BolusKind.Algorithm),
                6 => (BolusType.Normal, true, BolusKind.Algorithm),
                1 or 5 => (BolusType.Dual, false, BolusKind.Manual),
                _ => (BolusType.Normal, false, BolusKind.Manual)
            };

            if (bolusType == BolusType.Dual)
            {
                var durationMinutes = group.Msg2.GetUInt16("Duration");
                if (durationMinutes > 0)
                    duration = durationMinutes;
            }
        }

        var seqNums = new List<string> { group.Completed.SeqNum.ToString() };
        if (group.Msg1 != null) seqNums.Add(group.Msg1.SeqNum.ToString());
        if (group.Msg2 != null) seqNums.Add(group.Msg2.SeqNum.ToString());
        if (group.Msg3 != null) seqNums.Add(group.Msg3.SeqNum.ToString());

        var now = DateTime.UtcNow;
        return new Bolus
        {
            Id = Guid.CreateVersion7(),
            Timestamp = timestamp,
            Insulin = delivered,
            Programmed = requested,
            Delivered = delivered,
            BolusType = bolusType,
            Automatic = automatic,
            Kind = kind,
            Duration = duration,
            DataSource = DataSources.TandemSourceConnector,
            PumpRecordId = string.Join(",", seqNums),
            CreatedAt = now,
            ModifiedAt = now
        };
    }

    private class BolusEventGroup
    {
        public ParsedEvent? Msg1 { get; set; }
        public ParsedEvent? Msg2 { get; set; }
        public ParsedEvent? Msg3 { get; set; }
        public ParsedEvent? Completed { get; set; }
        public ParsedEvent? BolexCompleted { get; set; }
    }
}
