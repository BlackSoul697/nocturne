using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nocturne.API.Services.Alerts.Evaluators;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;
using Xunit;

namespace Nocturne.API.Tests.Services.Alerts.Evaluators;

[Trait("Category", "Unit")]
public class TrackerAgeEvaluatorTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid DefId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly TrackerAgeEvaluator _sut;

    public TrackerAgeEvaluatorTests()
    {
        var timeProvider = new FakeTimeProvider(FixedNow);
        _sut = new TrackerAgeEvaluator(timeProvider);
    }

    [Fact]
    public void ConditionType_ShouldBeTrackerAge()
    {
        _sut.ConditionType.Should().Be(AlertConditionType.TrackerAge);
    }

    [Fact]
    public async Task NullTrackerSnapshots_ReturnsFalse()
    {
        var json = Serialize(new TrackerAgeCondition(DefId, ">", 48));
        var context = MakeContext(trackerSnapshots: null);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task NoMatchingSnapshot_ReturnsFalse()
    {
        var json = Serialize(new TrackerAgeCondition(DefId, ">", 48));
        var snapshot = MakeSnapshot(Guid.NewGuid(), isActive: true, startedAt: FixedNow.AddHours(-50));
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task InactiveTracker_ReturnsFalse()
    {
        var json = Serialize(new TrackerAgeCondition(DefId, ">", 48));
        var snapshot = MakeSnapshot(DefId, isActive: false, startedAt: FixedNow.AddHours(-50));
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task NullStartedAt_ReturnsFalse()
    {
        var json = Serialize(new TrackerAgeCondition(DefId, ">", 48));
        var snapshot = MakeSnapshot(DefId, isActive: true, startedAt: null);
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task AgeExceedsThreshold_ReturnsTrue()
    {
        var json = Serialize(new TrackerAgeCondition(DefId, ">", 48));
        var snapshot = MakeSnapshot(DefId, isActive: true, startedAt: FixedNow.AddHours(-50));
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task AgeBelowThreshold_ReturnsFalse()
    {
        var json = Serialize(new TrackerAgeCondition(DefId, ">", 48));
        var snapshot = MakeSnapshot(DefId, isActive: true, startedAt: FixedNow.AddHours(-24));
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    private static string Serialize(TrackerAgeCondition c) =>
        JsonSerializer.Serialize(c, EvaluatorJson.Options);

    private static TrackerSnapshot MakeSnapshot(
        Guid definitionId, bool isActive, DateTimeOffset? startedAt) =>
        new(definitionId, Guid.NewGuid(), TrackerCategory.Consumable, TrackerMode.Duration,
            startedAt, null, 72m, isActive);

    private static SensorContext MakeContext(IReadOnlyList<TrackerSnapshot>? trackerSnapshots) => new()
    {
        LatestValue = 100m,
        LatestTimestamp = FixedNow.UtcDateTime,
        TrendRate = 0m,
        LastReadingAt = FixedNow.UtcDateTime,
        TrackerSnapshots = trackerSnapshots
    };
}
