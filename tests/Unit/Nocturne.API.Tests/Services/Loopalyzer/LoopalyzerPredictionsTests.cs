using FluentAssertions;
using Nocturne.API.Services.Loopalyzer;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.API.Tests.Services.Loopalyzer;

public class LoopalyzerPredictionsTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly DateOnly Day = new(2026, 5, 1);

    private static ApsSnapshot Snap(DateTime utc, bool enacted = true,
        string? iob = null, string? zt = null, string? cob = null, string? uam = null, string? def = null,
        DateTime? predStart = null)
        => new()
        {
            Timestamp = DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            Enacted = enacted,
            PredictedIobJson = iob,
            PredictedZtJson = zt,
            PredictedCobJson = cob,
            PredictedUamJson = uam,
            PredictedDefaultJson = def,
            PredictedStartTimestamp = predStart.HasValue ? DateTime.SpecifyKind(predStart.Value, DateTimeKind.Utc) : null,
        };

    [Fact]
    public void Predictions_OmitSnapshotsWithoutAnyCurve()
    {
        var snaps = new[] { Snap(new DateTime(2026, 5, 1, 8, 0, 0)) };

        var preds = LoopalyzerPredictions.Predictions(snaps, Day, Utc);

        preds.Should().BeEmpty();
    }

    [Fact]
    public void Predictions_EmitsOnePerSnapshot_WithIobCurve()
    {
        var snaps = new[]
        {
            Snap(new DateTime(2026, 5, 1, 8, 0, 0), iob: "[120, 122, 125]"),
            Snap(new DateTime(2026, 5, 1, 8, 5, 0), iob: "[122, 124, 126]"),
        };

        var preds = LoopalyzerPredictions.Predictions(snaps, Day, Utc);

        preds.Should().HaveCount(2);
        preds[0].Iob.Should().BeEquivalentTo(new[] { 120.0, 122.0, 125.0 });
        preds[1].Minute.Should().Be(485);
    }

    [Fact]
    public void Predictions_LoopShapeMappedToIobSlot()
    {
        var snaps = new[]
        {
            Snap(new DateTime(2026, 5, 1, 8, 0, 0), def: "[120, 121]"),
        };

        var preds = LoopalyzerPredictions.Predictions(snaps, Day, Utc);

        preds.Should().HaveCount(1);
        preds[0].Iob.Should().NotBeNull();
        preds[0].Zt.Should().BeNull();
    }

    [Fact]
    public void Predictions_RespectsPredictedStartTimestamp()
    {
        var snap = Snap(
            new DateTime(2026, 5, 1, 8, 0, 0),
            iob: "[1,2,3]",
            predStart: new DateTime(2026, 5, 1, 8, 30, 0));

        var preds = LoopalyzerPredictions.Predictions(new[] { snap }, Day, Utc);

        preds[0].Minute.Should().Be(510); // 08:30
    }

    [Fact]
    public void Bands_GroupsConsecutiveSameMode()
    {
        var snaps = new[]
        {
            Snap(new DateTime(2026, 5, 1, 0, 0, 0), enacted: true),
            Snap(new DateTime(2026, 5, 1, 0, 5, 0), enacted: true),
            Snap(new DateTime(2026, 5, 1, 0, 10, 0), enacted: false),
            Snap(new DateTime(2026, 5, 1, 0, 15, 0), enacted: false),
            Snap(new DateTime(2026, 5, 1, 0, 20, 0), enacted: true),
        };

        var bands = LoopalyzerPredictions.Bands(snaps, Day, Utc);

        bands.Should().HaveCount(3);
        bands[0].Mode.Should().Be("Closed");
        bands[1].Mode.Should().Be("Open");
        bands[2].Mode.Should().Be("Closed");
    }

    [Fact]
    public void Bands_FiltersToRequestedDay()
    {
        var snaps = new[]
        {
            Snap(new DateTime(2026, 4, 30, 23, 30, 0), enacted: true),
            Snap(new DateTime(2026, 5, 1, 0, 0, 0), enacted: true),
        };

        var bands = LoopalyzerPredictions.Bands(snaps, Day, Utc);

        bands.Should().HaveCount(1);
        bands[0].StartMinute.Should().Be(0);
    }
}
