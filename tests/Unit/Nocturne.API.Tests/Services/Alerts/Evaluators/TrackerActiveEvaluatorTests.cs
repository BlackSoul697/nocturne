using System.Text.Json;
using FluentAssertions;
using Nocturne.API.Services.Alerts.Evaluators;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;
using Xunit;

namespace Nocturne.API.Tests.Services.Alerts.Evaluators;

[Trait("Category", "Unit")]
public class TrackerActiveEvaluatorTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid DefId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private readonly TrackerActiveEvaluator _sut = new();

    [Fact]
    public void ConditionType_ShouldBeTrackerActive()
    {
        _sut.ConditionType.Should().Be(AlertConditionType.TrackerActive);
    }

    [Fact]
    public async Task NullTrackerSnapshots_ReturnsFalse()
    {
        var json = Serialize(new TrackerActiveCondition(DefId, true));
        var context = MakeContext(trackerSnapshots: null);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task NoMatchingSnapshot_ReturnsFalse()
    {
        var json = Serialize(new TrackerActiveCondition(DefId, true));
        var snapshot = MakeSnapshot(Guid.NewGuid(), isActive: true);
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task ActiveMatchesExpected_ReturnsTrue()
    {
        var json = Serialize(new TrackerActiveCondition(DefId, true));
        var snapshot = MakeSnapshot(DefId, isActive: true);
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task ActiveDoesNotMatchExpected_ReturnsFalse()
    {
        var json = Serialize(new TrackerActiveCondition(DefId, true));
        var snapshot = MakeSnapshot(DefId, isActive: false);
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task ExpectedFalseAndInactive_ReturnsTrue()
    {
        var json = Serialize(new TrackerActiveCondition(DefId, false));
        var snapshot = MakeSnapshot(DefId, isActive: false);
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeTrue();
    }

    private static string Serialize(TrackerActiveCondition c) =>
        JsonSerializer.Serialize(c, EvaluatorJson.Options);

    private static TrackerSnapshot MakeSnapshot(Guid definitionId, bool isActive) =>
        new(definitionId, Guid.NewGuid(), TrackerCategory.Consumable, TrackerMode.Duration,
            FixedNow.AddHours(-24), null, 72m, isActive);

    private static SensorContext MakeContext(IReadOnlyList<TrackerSnapshot>? trackerSnapshots) => new()
    {
        LatestValue = 100m,
        LatestTimestamp = FixedNow.UtcDateTime,
        TrendRate = 0m,
        LastReadingAt = FixedNow.UtcDateTime,
        TrackerSnapshots = trackerSnapshots
    };
}
