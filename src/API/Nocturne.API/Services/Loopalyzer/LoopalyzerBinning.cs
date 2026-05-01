using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.Loopalyzer;

/// <summary>
/// Resolves a scheduled basal rate at a given UTC instant. Implemented by
/// <c>TherapyTimeline.SnapshotAt(t).BasalRateAt(t)</c> in the live service;
/// tests pass a stub.
/// </summary>
internal delegate double ScheduledBasalAt(long mills);

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

    /// <summary>
    /// Bin the scheduled basal rate for each 5-minute tick of <paramref name="day"/>.
    /// The bin midpoint (offset by 2.5 minutes) is converted to a UTC instant via
    /// <paramref name="tz"/> and resolved via <paramref name="resolve"/>.
    /// </summary>
    public static double[] BinScheduledBasal(DateOnly day, TimeZoneInfo tz, ScheduledBasalAt resolve)
    {
        var bins = new double[BinsPerDay];
        var localMidnight = new DateTime(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Unspecified);

        for (var i = 0; i < BinsPerDay; i++)
        {
            // Midpoint of the 5-minute bin in local time.
            var localMidpoint = localMidnight.AddMinutes(i * MinutesPerBin + (MinutesPerBin / 2.0));
            var utc = TimeZoneInfo.ConvertTimeToUtc(localMidpoint, tz);
            var mills = new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();
            bins[i] = resolve(mills);
        }

        return bins;
    }

    /// <summary>
    /// Bin actively-running temp basals across the day. A bin's value is the rate of the
    /// most-recently-started temp basal active at the bin midpoint, or <c>null</c> when no
    /// temp is running. Suspended temps surface as their literal <see cref="TempBasal.Rate"/>
    /// (typically 0).
    /// </summary>
    /// <summary>
    /// Iterate the 5-minute bins of <paramref name="day"/> in patient TZ, calling
    /// <paramref name="resolve"/> with the bin midpoint UTC mills. Returns an array
    /// of resolved values; bins where <paramref name="resolve"/> returns <c>null</c>
    /// remain <c>null</c>.
    /// </summary>
    public static double?[] BinByMidpoint(DateOnly day, TimeZoneInfo tz, Func<long, double?> resolve)
    {
        var bins = new double?[BinsPerDay];
        var localMidnight = new DateTime(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Unspecified);
        for (var i = 0; i < BinsPerDay; i++)
        {
            var localMidpoint = localMidnight.AddMinutes(i * MinutesPerBin + (MinutesPerBin / 2.0));
            var utc = TimeZoneInfo.ConvertTimeToUtc(localMidpoint, tz);
            var mills = new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();
            bins[i] = resolve(mills);
        }
        return bins;
    }

    public static double?[] BinTempBasal(IEnumerable<TempBasal> tempBasals, DateOnly day, TimeZoneInfo tz)
    {
        var bins = new double?[BinsPerDay];
        var localMidnight = new DateTime(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Unspecified);

        // Sort newest-first so the most recent active temp wins per tick.
        var ordered = tempBasals.OrderByDescending(tb => tb.StartMills).ToList();

        for (var i = 0; i < BinsPerDay; i++)
        {
            var localMidpoint = localMidnight.AddMinutes(i * MinutesPerBin + (MinutesPerBin / 2.0));
            var utc = TimeZoneInfo.ConvertTimeToUtc(localMidpoint, tz);
            var mills = new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();

            foreach (var tb in ordered)
            {
                if (tb.StartMills > mills)
                    continue;
                var endMills = tb.EndMills ?? long.MaxValue;
                if (mills < endMills)
                {
                    bins[i] = tb.Rate;
                    break;
                }
            }
        }

        return bins;
    }
}
