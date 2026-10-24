using System.Globalization;

namespace Nocturne.Core.Models.V4;

/// <summary>When in the day a <see cref="ScheduleEntry"/> or <see cref="TargetRangeEntry"/> starts.</summary>
public static class ScheduleEntryTime
{
    private static readonly string[] Formats = ["H:mm", "H:mm:ss"];

    /// <summary>Seconds from midnight: <paramref name="timeAsSeconds"/>, else <paramref name="time"/> parsed, else 0.</summary>
    public static int SecondsOf(int? timeAsSeconds, string time) =>
        timeAsSeconds ?? (TryParseSeconds(time, out var parsed) ? parsed : 0);

    /// <summary>
    /// Parses a time of day written <c>H:mm</c> or <c>H:mm:ss</c>, in the invariant culture. A bare
    /// number such as "6" is not a time of day.
    /// </summary>
    public static bool TryParseSeconds(string? time, out int seconds)
    {
        if (TimeOnly.TryParseExact(time, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            seconds = (int)parsed.ToTimeSpan().TotalSeconds;
            return true;
        }

        seconds = 0;
        return false;
    }
}
