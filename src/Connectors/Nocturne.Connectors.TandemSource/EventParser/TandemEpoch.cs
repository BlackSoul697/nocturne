namespace Nocturne.Connectors.TandemSource.EventParser;

public static class TandemEpoch
{
    /// <summary>
    /// Tandem pump epoch: 2008-01-01T00:00:00Z as Unix timestamp
    /// </summary>
    public const long EpochSeconds = 1199145600;

    public static readonly DateTimeOffset EpochDateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(EpochSeconds);

    /// <summary>
    /// Converts Tandem raw timestamp (seconds since Tandem epoch) to a DateTimeOffset
    /// in the specified timezone. Pump timestamps have no inherent timezone; the user's
    /// configured timezone is applied as wall-clock time.
    /// </summary>
    public static DateTimeOffset ToDateTimeOffset(uint rawTimestamp, TimeZoneInfo timezone)
    {
        var utcDateTime = EpochDateTimeOffset.AddSeconds(rawTimestamp).UtcDateTime;
        var offset = timezone.GetUtcOffset(utcDateTime);
        return new DateTimeOffset(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Unspecified), offset);
    }

    /// <summary>
    /// Converts Tandem raw timestamp to UTC DateTime, treating the raw time as wall-clock
    /// time in the given timezone and converting to UTC.
    /// </summary>
    public static DateTime ToUtcDateTime(uint rawTimestamp, TimeZoneInfo timezone)
    {
        var wallClockTime = EpochDateTimeOffset.AddSeconds(rawTimestamp).UtcDateTime;
        var localTime = DateTime.SpecifyKind(wallClockTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localTime, timezone);
    }
}
