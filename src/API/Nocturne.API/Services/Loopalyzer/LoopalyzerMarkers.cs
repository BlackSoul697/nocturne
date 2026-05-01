using Nocturne.Core.Models;
using Nocturne.Core.Models.Loopalyzer;

namespace Nocturne.API.Services.Loopalyzer;

/// <summary>
/// Pure helpers that project <see cref="Treatment"/> records into the marker
/// collections carried by <see cref="LoopalyzerDay"/>.
/// </summary>
internal static class LoopalyzerMarkers
{
    private const string SiteChange = "Site Change";
    private const string SensorChange = "Sensor Change";

    /// <summary>
    /// Carb-bearing treatments converted to <see cref="LoopalyzerMeal"/>. Frontends
    /// filter by event-type per Grill 7; backend returns the full set.
    /// </summary>
    public static IReadOnlyList<LoopalyzerMeal> Meals(IEnumerable<Treatment> treatments, DateOnly day, TimeZoneInfo tz)
    {
        var list = new List<LoopalyzerMeal>();
        foreach (var t in treatments)
        {
            if (!(t.Carbs is > 0))
                continue;
            if (!TryLocalMinute(t.Mills, day, tz, out var minute))
                continue;
            list.Add(new LoopalyzerMeal(minute, t.Carbs!.Value, t.EventType ?? string.Empty));
        }
        return list;
    }

    /// <summary>Insulin-bearing treatments converted to <see cref="LoopalyzerBolus"/>.</summary>
    public static IReadOnlyList<LoopalyzerBolus> Boluses(IEnumerable<Treatment> treatments, DateOnly day, TimeZoneInfo tz)
    {
        var list = new List<LoopalyzerBolus>();
        foreach (var t in treatments)
        {
            if (!(t.Insulin is > 0))
                continue;
            if (!TryLocalMinute(t.Mills, day, tz, out var minute))
                continue;
            list.Add(new LoopalyzerBolus(minute, t.Insulin!.Value));
        }
        return list;
    }

    public static IReadOnlyList<LoopalyzerSiteEvent> SiteChanges(IEnumerable<Treatment> treatments, DateOnly day, TimeZoneInfo tz)
        => SiteEventsByType(treatments, day, tz, SiteChange);

    public static IReadOnlyList<LoopalyzerSiteEvent> SensorChanges(IEnumerable<Treatment> treatments, DateOnly day, TimeZoneInfo tz)
        => SiteEventsByType(treatments, day, tz, SensorChange);

    private static IReadOnlyList<LoopalyzerSiteEvent> SiteEventsByType(
        IEnumerable<Treatment> treatments, DateOnly day, TimeZoneInfo tz, string eventType)
    {
        var list = new List<LoopalyzerSiteEvent>();
        foreach (var t in treatments)
        {
            if (!string.Equals(t.EventType, eventType, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!TryLocalMinute(t.Mills, day, tz, out var minute))
                continue;
            list.Add(new LoopalyzerSiteEvent(minute, t.Notes));
        }
        return list;
    }

    private static bool TryLocalMinute(long mills, DateOnly day, TimeZoneInfo tz, out int minuteOfDay)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(mills), tz);
        if (DateOnly.FromDateTime(local.DateTime) != day)
        {
            minuteOfDay = -1;
            return false;
        }
        minuteOfDay = (int)local.TimeOfDay.TotalMinutes;
        return true;
    }
}
