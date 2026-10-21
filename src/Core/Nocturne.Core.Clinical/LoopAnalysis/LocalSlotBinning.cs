namespace Nocturne.Core.Clinical.LoopAnalysis;

/// <summary>
/// Places instants and half-open intervals on a zone's local wall clock, in N-minute time-of-day
/// slots. Every time-of-day distribution goes through here.
/// </summary>
public static class LocalSlotBinning
{
    public const long MinuteMs = 60_000L;

    /// <summary>
    /// Walks <c>[startMills, endMills)</c> in slices that end on <paramref name="tz"/>'s local
    /// <paramref name="slotMinutes"/>-minute boundaries, handing each slice's local start and length
    /// to <paramref name="addSlice"/>. With hour slots, a 30-minute interval at local 13:55 yields
    /// 5 min in hour 13 and 25 min in hour 14. Slicing on local rather than UTC boundaries is what
    /// keeps a zone with a non-whole-hour offset (Adelaide +9:30, Kathmandu +5:45) from attributing
    /// part of every slice to the neighbouring slot.
    /// </summary>
    /// <remarks>
    /// The boundary is computed with the offset in force at the slice start, so a DST transition
    /// that falls on a slot boundary ends a slice exactly there: a spring-forward day yields no
    /// slice for the skipped slots, and a fall-back day yields two for each repeated one.
    /// </remarks>
    public static void Distribute(
        long startMills,
        long endMills,
        TimeZoneInfo tz,
        int slotMinutes,
        Action<DateTime, long> addSlice) =>
        Distribute(startMills, endMills, tz, slotMinutes, (_, local, sliceMs) => addSlice(local, sliceMs));

    /// <inheritdoc cref="Distribute(long, long, TimeZoneInfo, int, Action{DateTime, long})"/>
    /// <param name="addSlice">Takes the slice's start instant as well as its local start and length.</param>
    public static void Distribute(
        long startMills,
        long endMills,
        TimeZoneInfo tz,
        int slotMinutes,
        Action<long, DateTime, long> addSlice)
    {
        var slotMs = slotMinutes * MinuteMs;
        long cursor = startMills;
        while (cursor < endMills)
        {
            var utcDt = DateTimeOffset.FromUnixTimeMilliseconds(cursor);
            var offsetMs = (long)tz.GetUtcOffset(utcDt).TotalMilliseconds;
            long nextLocalBoundary = ((cursor + offsetMs) / slotMs + 1) * slotMs - offsetMs;
            long sliceEnd = Math.Min(nextLocalBoundary, endMills);
            addSlice(cursor, TimeZoneInfo.ConvertTimeFromUtc(utcDt.UtcDateTime, tz), sliceEnd - cursor);
            cursor = sliceEnd;
        }
    }

    public static DateTime LocalTime(long mills, TimeZoneInfo tz) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime, tz);

    public static DateOnly LocalDay(long mills, TimeZoneInfo tz) => DateOnly.FromDateTime(LocalTime(mills, tz));

    /// <summary>The slot of the day <paramref name="local"/> falls in, counted from local midnight.</summary>
    public static int SlotOf(DateTime local, int slotMinutes) => (local.Hour * 60 + local.Minute) / slotMinutes;

    /// <summary>
    /// The UTC instant local <paramref name="day"/> begins in <paramref name="tz"/>. A zone whose
    /// midnight is skipped by a DST transition begins the day at the first instant after it.
    /// </summary>
    public static long DayStartMills(DateOnly day, TimeZoneInfo tz)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (tz.IsInvalidTime(local))
            local = local.AddMinutes(1);
        var offset = tz.IsAmbiguousTime(local)
            ? tz.GetAmbiguousTimeOffsets(local).Max()
            : tz.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUnixTimeMilliseconds();
    }
}
