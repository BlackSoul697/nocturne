namespace Nocturne.Widget.Contracts;

/// <summary>What an AlarmHub event means to a client: raise it (urgent or not) or clear it.</summary>
public readonly record struct AlarmEventKind(bool Urgent, bool Cleared);

/// <summary>
/// The AlarmHub events a client listens for. The engine names mirror <c>EngineAlarmEvents</c> on the
/// server, which sends the same notify payload under them as under the legacy names.
/// </summary>
public static class AlarmHubEvents
{
    /// <summary>Each listened-for event name and its kind.</summary>
    public static IReadOnlyDictionary<string, AlarmEventKind> Kinds { get; } =
        new Dictionary<string, AlarmEventKind>
        {
            ["alarm"] = new(Urgent: false, Cleared: false),
            ["urgent_alarm"] = new(Urgent: true, Cleared: false),
            ["clear_alarm"] = new(Urgent: false, Cleared: true),
            ["engine_alarm"] = new(Urgent: false, Cleared: false),
            ["engine_urgent_alarm"] = new(Urgent: true, Cleared: false),
            ["engine_clear_alarm"] = new(Urgent: false, Cleared: true),
        };
}
