using Nocturne.Core.Models;

namespace Nocturne.API.Services.Loopalyzer;

/// <summary>
/// Pure helpers that bin time-series points into the 288 5-minute slots that make up
/// a Loopalyzer day. All binning is in patient-local time; callers convert UTC instants
/// via the supplied <see cref="TimeZoneInfo"/>.
/// </summary>
internal static class LoopalyzerBinning
{
    public const int BinsPerDay = 288;
    public const int MinutesPerBin = 5;

    /// <summary>
    /// Bin sensor glucose entries that fall within <paramref name="day"/> (patient TZ) into a
    /// length-288 array of nullable doubles. When multiple entries fall in the same bin the
    /// chronologically last entry wins. Empty bins remain <c>null</c>.
    /// </summary>
    public static double?[] BinSgvs(IEnumerable<Entry> entries, DateOnly day, TimeZoneInfo tz)
    {
        var bins = new double?[BinsPerDay];

        // Process in chronological order so "last wins" is just "last assignment wins".
        foreach (var entry in entries.OrderBy(e => e.Mills))
        {
            var localTime = TimeZoneInfo.ConvertTime(
                DateTimeOffset.FromUnixTimeMilliseconds(entry.Mills),
                tz);

            if (DateOnly.FromDateTime(localTime.DateTime) != day)
                continue;

            var minuteOfDay = (int)localTime.TimeOfDay.TotalMinutes;
            var binIndex = minuteOfDay / MinutesPerBin;
            if (binIndex < 0 || binIndex >= BinsPerDay)
                continue;

            var sgv = entry.Sgv ?? entry.Mgdl;
            if (sgv > 0)
                bins[binIndex] = sgv;
        }

        return bins;
    }
}
