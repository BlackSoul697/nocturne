using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.Core.Clinical.LoopAnalysis;

/// <summary>
/// Resolves Loop Analysis ranges of tenant-local calendar dates to the instants they span. A day is
/// a local day, so a DST day stays whole: 23 or 25 hours.
/// </summary>
public static class LoopAnalysisRanges
{
    /// <summary>
    /// <paramref name="startDate"/> to <paramref name="endDate"/> inclusive, keeping only the most
    /// recent <paramref name="maxDays"/> local days when it spans more.
    /// </summary>
    public static LoopAnalysisResolvedRange Resolve(
        DateOnly startDate, DateOnly endDate, TimeZoneInfo tz, int maxDays)
    {
        var shortened = endDate.DayNumber - startDate.DayNumber + 1 > maxDays;
        return Of(shortened ? endDate.AddDays(1 - maxDays) : startDate, endDate, tz, shortened);
    }

    /// <summary>The same number of days as <paramref name="primary"/>, ending the day before it starts.</summary>
    public static LoopAnalysisResolvedRange Previous(LoopAnalysisResolvedRange primary, TimeZoneInfo tz)
    {
        var days = primary.EndDate.DayNumber - primary.StartDate.DayNumber + 1;
        return Of(primary.StartDate.AddDays(-days), primary.StartDate.AddDays(-1), tz, shortened: false);
    }

    /// <summary>The last <paramref name="window"/> before <paramref name="nowUtc"/>, for a history-clamped caller.</summary>
    public static LoopAnalysisResolvedRange Clamped(DateTime nowUtc, TimeSpan window, TimeZoneInfo tz)
    {
        var from = nowUtc - window;
        var toMills = new DateTimeOffset(nowUtc, TimeSpan.Zero).ToUnixTimeMilliseconds();
        return new LoopAnalysisResolvedRange
        {
            StartDate = LocalSlotBinning.LocalDay(new DateTimeOffset(from, TimeSpan.Zero).ToUnixTimeMilliseconds(), tz),
            EndDate = LocalSlotBinning.LocalDay(toMills - 1, tz),
            From = from,
            To = nowUtc,
        };
    }

    private static LoopAnalysisResolvedRange Of(DateOnly startDate, DateOnly endDate, TimeZoneInfo tz, bool shortened) => new()
    {
        StartDate = startDate,
        EndDate = endDate,
        From = DateTimeOffset.FromUnixTimeMilliseconds(LocalSlotBinning.DayStartMills(startDate, tz)).UtcDateTime,
        To = DateTimeOffset.FromUnixTimeMilliseconds(LocalSlotBinning.DayStartMills(endDate.AddDays(1), tz)).UtcDateTime,
        Shortened = shortened,
    };
}
