using System.Text.Json;
using Nocturne.Core.Models.Loopalyzer;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.Loopalyzer;

/// <summary>
/// Pure helpers for projecting <see cref="ApsSnapshot"/> rows into the per-cycle
/// <see cref="LoopalyzerPrediction"/> records and continuous APS-mode
/// <see cref="LoopalyzerApsBand"/> regions surfaced in single-day mode only.
/// </summary>
internal static class LoopalyzerPredictions
{
    /// <summary>
    /// Emit one <see cref="LoopalyzerPrediction"/> per snapshot that has at least one
    /// non-empty prediction curve. The minute is computed from
    /// <see cref="ApsSnapshot.PredictedStartTimestamp"/> when present, otherwise
    /// <see cref="ApsSnapshot.Timestamp"/>.
    /// </summary>
    public static IReadOnlyList<LoopalyzerPrediction> Predictions(
        IEnumerable<ApsSnapshot> snapshots, DateOnly day, TimeZoneInfo tz)
    {
        var list = new List<LoopalyzerPrediction>();
        foreach (var s in snapshots)
        {
            var iob = ParseArray(s.PredictedIobJson);
            var zt = ParseArray(s.PredictedZtJson);
            var cob = ParseArray(s.PredictedCobJson);
            var uam = ParseArray(s.PredictedUamJson);
            var defaults = ParseArray(s.PredictedDefaultJson);

            // Loop-style payloads carry the curve as PredictedDefaultJson; map into the IOB slot
            // for visualization purposes only (frontend renders any non-null curve).
            if (iob is null && zt is null && cob is null && uam is null && defaults is null)
                continue;

            var anchorMills = s.PredictedStartMills
                ?? new DateTimeOffset(s.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();
            if (!TryLocalMinute(anchorMills, day, tz, out var minute))
                continue;

            list.Add(new LoopalyzerPrediction(minute, iob ?? defaults, zt, cob, uam));
        }
        return list;
    }

    /// <summary>
    /// Group consecutive snapshots into APS-mode bands. Mode is derived from
    /// <see cref="ApsSnapshot.Enacted"/>: enacted=true → "Closed"; enacted=false → "Open".
    /// Snapshots without algorithm metadata are skipped.
    /// </summary>
    public static IReadOnlyList<LoopalyzerApsBand> Bands(
        IEnumerable<ApsSnapshot> snapshots, DateOnly day, TimeZoneInfo tz)
    {
        var list = new List<LoopalyzerApsBand>();
        string? currentMode = null;
        int? bandStart = null;
        int? bandEnd = null;

        foreach (var s in snapshots.OrderBy(s => s.Timestamp))
        {
            var mills = new DateTimeOffset(s.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();
            if (!TryLocalMinute(mills, day, tz, out var minute))
                continue;

            var mode = s.Enacted ? "Closed" : "Open";
            if (mode != currentMode)
            {
                if (currentMode is not null && bandStart.HasValue && bandEnd.HasValue)
                    list.Add(new LoopalyzerApsBand(bandStart.Value, bandEnd.Value, currentMode));
                currentMode = mode;
                bandStart = minute;
            }
            bandEnd = minute;
        }

        if (currentMode is not null && bandStart.HasValue && bandEnd.HasValue)
            list.Add(new LoopalyzerApsBand(bandStart.Value, bandEnd.Value, currentMode));

        return list;
    }

    private static double[]? ParseArray(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            var array = JsonSerializer.Deserialize<double[]>(json);
            return array is { Length: > 0 } ? array : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryLocalMinute(long mills, DateOnly day, TimeZoneInfo tz, out int minute)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(mills), tz);
        if (DateOnly.FromDateTime(local.DateTime) != day)
        {
            minute = -1;
            return false;
        }
        minute = (int)local.TimeOfDay.TotalMinutes;
        return true;
    }
}
