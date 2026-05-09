using Nocturne.Core.Models;
using Nocturne.Core.Models.Loopalyzer;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.Loopalyzer;

internal static class LoopalyzerMarkers
{
    public static IReadOnlyList<LoopalyzerMeal> Meals(IEnumerable<CarbIntake> carbIntakes, DateOnly day, TimeZoneInfo tz)
    {
        var list = new List<LoopalyzerMeal>();
        foreach (var c in carbIntakes)
        {
            if (c.Carbs <= 0) continue;
            if (!TryLocalMinute(c.Mills, day, tz, out var minute)) continue;
            list.Add(new LoopalyzerMeal(minute, c.Carbs, string.Empty));
        }
        return list;
    }

    public static IReadOnlyList<LoopalyzerBolus> Boluses(IEnumerable<Bolus> boluses, DateOnly day, TimeZoneInfo tz)
    {
        var list = new List<LoopalyzerBolus>();
        foreach (var b in boluses)
        {
            if (b.Insulin <= 0) continue;
            if (!TryLocalMinute(b.Mills, day, tz, out var minute)) continue;
            list.Add(new LoopalyzerBolus(minute, b.Insulin));
        }
        return list;
    }

    public static IReadOnlyList<LoopalyzerSiteEvent> SiteChanges(IEnumerable<DeviceEvent> events, DateOnly day, TimeZoneInfo tz)
        => FilterDeviceEvents(events, day, tz, DeviceEventType.SiteChange);

    public static IReadOnlyList<LoopalyzerSiteEvent> SensorChanges(IEnumerable<DeviceEvent> events, DateOnly day, TimeZoneInfo tz)
        => FilterDeviceEvents(events, day, tz, DeviceEventType.SensorChange);

    private static IReadOnlyList<LoopalyzerSiteEvent> FilterDeviceEvents(
        IEnumerable<DeviceEvent> events, DateOnly day, TimeZoneInfo tz, DeviceEventType eventType)
    {
        var list = new List<LoopalyzerSiteEvent>();
        foreach (var e in events)
        {
            if (e.EventType != eventType) continue;
            if (!TryLocalMinute(e.Mills, day, tz, out var minute)) continue;
            list.Add(new LoopalyzerSiteEvent(minute, e.Notes));
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
