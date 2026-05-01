using FluentAssertions;
using Nocturne.API.Services.Loopalyzer;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services.Loopalyzer;

public class LoopalyzerMarkersTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly DateOnly Day = new(2026, 5, 1);

    private static Treatment Tx(DateTime utc, string? eventType = null, double? carbs = null, double? insulin = null, string? notes = null)
        => new()
        {
            Mills = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds(),
            EventType = eventType,
            Carbs = carbs,
            Insulin = insulin,
            Notes = notes,
        };

    [Fact]
    public void Meals_IncludesAllCarbBearingTreatments()
    {
        var treatments = new[]
        {
            Tx(new DateTime(2026, 5, 1, 8, 0, 0), eventType: "Meal Bolus", carbs: 30, insulin: 3),
            Tx(new DateTime(2026, 5, 1, 12, 0, 0), eventType: "Snack", carbs: 15),
            Tx(new DateTime(2026, 5, 1, 14, 0, 0), eventType: "Correction Bolus", insulin: 1.5),  // no carbs
        };

        var meals = LoopalyzerMarkers.Meals(treatments, Day, Utc);

        meals.Should().HaveCount(2);
        meals[0].Minute.Should().Be(480);
        meals[0].Carbs.Should().Be(30);
        meals[0].EventType.Should().Be("Meal Bolus");
    }

    [Fact]
    public void Boluses_IncludesAllInsulinBearingTreatments()
    {
        var treatments = new[]
        {
            Tx(new DateTime(2026, 5, 1, 8, 0, 0), insulin: 3),
            Tx(new DateTime(2026, 5, 1, 12, 0, 0), insulin: 0),
            Tx(new DateTime(2026, 5, 1, 14, 0, 0), carbs: 20),
        };

        var boluses = LoopalyzerMarkers.Boluses(treatments, Day, Utc);

        boluses.Should().HaveCount(1);
        boluses[0].Units.Should().Be(3);
    }

    [Fact]
    public void SiteChanges_FiltersByEventType()
    {
        var treatments = new[]
        {
            Tx(new DateTime(2026, 5, 1, 7, 30, 0), eventType: "Site Change", notes: "left arm"),
            Tx(new DateTime(2026, 5, 1, 8, 0, 0), eventType: "Sensor Change"),
        };

        var sites = LoopalyzerMarkers.SiteChanges(treatments, Day, Utc);
        var sensors = LoopalyzerMarkers.SensorChanges(treatments, Day, Utc);

        sites.Should().HaveCount(1);
        sites[0].Note.Should().Be("left arm");
        sensors.Should().HaveCount(1);
    }

    [Fact]
    public void Markers_ExcludeTreatmentsOutsideDay()
    {
        var treatments = new[]
        {
            Tx(new DateTime(2026, 4, 30, 23, 30, 0), eventType: "Meal", carbs: 30),
            Tx(new DateTime(2026, 5, 2, 0, 30, 0), eventType: "Meal", carbs: 40),
        };

        LoopalyzerMarkers.Meals(treatments, Day, Utc).Should().BeEmpty();
    }
}
