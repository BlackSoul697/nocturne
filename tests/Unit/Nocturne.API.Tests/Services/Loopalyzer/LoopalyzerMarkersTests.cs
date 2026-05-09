using FluentAssertions;
using Nocturne.API.Services.Loopalyzer;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.API.Tests.Services.Loopalyzer;

public class LoopalyzerMarkersTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly DateOnly Day = new(2026, 5, 1);

    private static CarbIntake Carb(DateTime utc, double carbs) => new()
    {
        Timestamp = DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        Carbs = carbs,
    };

    private static Bolus Bol(DateTime utc, double insulin) => new()
    {
        Timestamp = DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        Insulin = insulin,
    };

    private static DeviceEvent Dev(DateTime utc, DeviceEventType eventType, string? notes = null) => new()
    {
        Timestamp = DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        EventType = eventType,
        Notes = notes,
    };

    [Fact]
    public void Meals_IncludesAllCarbIntakes()
    {
        var carbs = new[]
        {
            Carb(new DateTime(2026, 5, 1, 8, 0, 0), 30),
            Carb(new DateTime(2026, 5, 1, 12, 0, 0), 15),
        };

        var meals = LoopalyzerMarkers.Meals(carbs, Day, Utc);

        meals.Should().HaveCount(2);
        meals[0].Minute.Should().Be(480);
        meals[0].Carbs.Should().Be(30);
    }

    [Fact]
    public void Meals_ExcludesZeroCarbs()
    {
        var carbs = new[] { Carb(new DateTime(2026, 5, 1, 8, 0, 0), 0) };
        LoopalyzerMarkers.Meals(carbs, Day, Utc).Should().BeEmpty();
    }

    [Fact]
    public void Boluses_IncludesAllInsulinBoluses()
    {
        var boluses = new[]
        {
            Bol(new DateTime(2026, 5, 1, 8, 0, 0), 3),
            Bol(new DateTime(2026, 5, 1, 12, 0, 0), 0),
        };

        var result = LoopalyzerMarkers.Boluses(boluses, Day, Utc);

        result.Should().HaveCount(1);
        result[0].Units.Should().Be(3);
    }

    [Fact]
    public void SiteAndSensorChanges_FilterByEventType()
    {
        var events = new[]
        {
            Dev(new DateTime(2026, 5, 1, 7, 30, 0), DeviceEventType.SiteChange, "left arm"),
            Dev(new DateTime(2026, 5, 1, 8, 0, 0), DeviceEventType.SensorChange),
        };

        var sites = LoopalyzerMarkers.SiteChanges(events, Day, Utc);
        var sensors = LoopalyzerMarkers.SensorChanges(events, Day, Utc);

        sites.Should().HaveCount(1);
        sites[0].Note.Should().Be("left arm");
        sensors.Should().HaveCount(1);
    }

    [Fact]
    public void Markers_ExcludeEventsOutsideDay()
    {
        var carbs = new[]
        {
            Carb(new DateTime(2026, 4, 30, 23, 30, 0), 30),
            Carb(new DateTime(2026, 5, 2, 0, 30, 0), 40),
        };

        LoopalyzerMarkers.Meals(carbs, Day, Utc).Should().BeEmpty();
    }
}
