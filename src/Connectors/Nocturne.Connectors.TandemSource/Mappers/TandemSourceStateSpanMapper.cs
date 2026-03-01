using Nocturne.Connectors.TandemSource.EventParser;
using Nocturne.Connectors.TandemSource.Models;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.TandemSource.Mappers;

/// <summary>
/// Maps suspend/resume and user mode (sleep/exercise) events to StateSpan records.
/// </summary>
public static class TandemSourceStateSpanMapper
{
    public static List<StateSpan> Map(
        Dictionary<EventClass, List<ParsedEvent>> classified,
        TimeZoneInfo timezone)
    {
        var result = new List<StateSpan>();

        if (classified.TryGetValue(EventClass.BasalSuspension, out var suspendEvents) ||
            classified.TryGetValue(EventClass.BasalResume, out _))
        {
            var resumeEvents = classified.GetValueOrDefault(EventClass.BasalResume) ?? [];
            result.AddRange(MapSuspendResume(suspendEvents ?? [], resumeEvents, timezone));
        }

        if (classified.TryGetValue(EventClass.UserMode, out var userModeEvents))
            result.AddRange(MapUserMode(userModeEvents, timezone));

        return result;
    }

    private static IEnumerable<StateSpan> MapSuspendResume(
        List<ParsedEvent> suspendEvents, List<ParsedEvent> resumeEvents, TimeZoneInfo timezone)
    {
        var resumeQueue = new Queue<ParsedEvent>(resumeEvents.OrderBy(e => e.TimestampRaw));

        foreach (var suspend in suspendEvents.OrderBy(e => e.TimestampRaw))
        {
            var startTime = suspend.GetUtcTimestamp(timezone);
            DateTime? endTime = null;

            while (resumeQueue.Count > 0 && resumeQueue.Peek().TimestampRaw <= suspend.TimestampRaw)
                resumeQueue.Dequeue();

            ParsedEvent? matchingResume = null;
            if (resumeQueue.Count > 0)
            {
                matchingResume = resumeQueue.Dequeue();
                endTime = matchingResume.GetUtcTimestamp(timezone);
            }

            var reasonRaw = suspend.HasField("SuspendReason") ? suspend.GetUInt8("SuspendReason") : (byte)0;
            var reason = reasonRaw switch
            {
                0 => "User Suspended",
                1 => "Alarm",
                2 => "Malfunction",
                6 => "Auto Suspend (PLGS)",
                _ => "Suspended"
            };

            var seqNums = suspend.SeqNum.ToString();
            if (matchingResume != null)
                seqNums += "," + matchingResume.SeqNum;

            yield return new StateSpan
            {
                Id = Guid.CreateVersion7().ToString(),
                Category = StateSpanCategory.PumpMode,
                State = "Suspended",
                StartTimestamp = startTime,
                EndTimestamp = endTime,
                Source = DataSources.TandemSourceConnector,
                OriginalId = seqNums,
                Metadata = new Dictionary<string, object> { ["reason"] = reason },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }

    private static IEnumerable<StateSpan> MapUserMode(List<ParsedEvent> events, TimeZoneInfo timezone)
    {
        var sorted = events.OrderBy(e => e.TimestampRaw).ToList();

        ParsedEvent? sleepStart = null;
        ParsedEvent? exerciseStart = null;

        foreach (var evt in sorted)
        {
            var requestedAction = evt.GetUInt8("RequestedAction");

            switch (requestedAction)
            {
                case 1: // Start Sleep
                    sleepStart = evt;
                    break;
                case 2: // Stop Sleep
                case 5: // Stop All
                    if (sleepStart != null)
                    {
                        yield return CreateUserModeSpan(sleepStart, evt, StateSpanCategory.Sleep, "Sleep", timezone);
                        sleepStart = null;
                    }
                    if (requestedAction == 5 && exerciseStart != null)
                    {
                        yield return CreateUserModeSpan(exerciseStart, evt, StateSpanCategory.Exercise, "Exercise", timezone);
                        exerciseStart = null;
                    }
                    break;
                case 3: // Start Exercise
                    exerciseStart = evt;
                    break;
                case 4: // Stop Exercise
                    if (exerciseStart != null)
                    {
                        yield return CreateUserModeSpan(exerciseStart, evt, StateSpanCategory.Exercise, "Exercise", timezone);
                        exerciseStart = null;
                    }
                    break;
            }
        }

        if (sleepStart != null)
            yield return CreateUserModeSpan(sleepStart, null, StateSpanCategory.Sleep, "Sleep", timezone);
        if (exerciseStart != null)
            yield return CreateUserModeSpan(exerciseStart, null, StateSpanCategory.Exercise, "Exercise", timezone);
    }

    private static StateSpan CreateUserModeSpan(
        ParsedEvent start, ParsedEvent? stop,
        StateSpanCategory category, string state,
        TimeZoneInfo timezone)
    {
        var seqNums = start.SeqNum.ToString();
        if (stop != null) seqNums += "," + stop.SeqNum;

        return new StateSpan
        {
            Id = Guid.CreateVersion7().ToString(),
            Category = category,
            State = state,
            StartTimestamp = start.GetUtcTimestamp(timezone),
            EndTimestamp = stop?.GetUtcTimestamp(timezone),
            Source = DataSources.TandemSourceConnector,
            OriginalId = seqNums,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
