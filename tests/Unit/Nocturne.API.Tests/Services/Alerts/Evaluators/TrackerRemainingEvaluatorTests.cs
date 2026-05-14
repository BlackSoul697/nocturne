using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nocturne.API.Services.Alerts.Evaluators;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;
using Xunit;

namespace Nocturne.API.Tests.Services.Alerts.Evaluators;

[Trait("Category", "Unit")]
public class TrackerRemainingEvaluatorTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid DefId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly TrackerRemainingEvaluator _sut;

    public TrackerRemainingEvaluatorTests()
    {
        var timeProvider = new FakeTimeProvider(FixedNow);
        _sut = new TrackerRemainingEvaluator(timeProvider);
    }

    [Fact]
    public void ConditionType_ShouldBeTrackerRemaining()
    {
        _sut.ConditionType.Should().Be(AlertConditionType.TrackerRemaining);
    }

    [Fact]
    public async Task NullTrackerSnapshots_ReturnsFalse()
    {
        var json = Serialize(new TrackerRemainingCondition(DefId, "<", 12));
        var context = MakeContext(trackerSnapshots: null);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task NoMatchingSnapshot_ReturnsFalse()
    {
        var json = Serialize(new TrackerRemainingCondition(DefId, "<", 12));
        var snapshot = MakeSnapshot(Guid.NewGuid(), isActive: true, startedAt: FixedNow.AddHours(-60), lifespanHours: 72);
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task InactiveTracker_ReturnsFalse()
    {
        var json = Serialize(new TrackerRemainingCondition(DefId, "<", 12));
        var snapshot = MakeSnapshot(DefId, isActive: false, startedAt: FixedNow.AddHours(-60), lifespanHours: 72);
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task NullStartedAt_ReturnsFalse()
    {
        var json = Serialize(new TrackerRemainingCondition(DefId, "<", 12));
        var snapshot = MakeSnapshot(DefId, isActive: true, startedAt: null, lifespanHours: 72);
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task NullLifespanHours_ReturnsFalse()
    {
        var json = Serialize(new TrackerRemainingCondition(DefId, "<", 12));
        var snapshot = MakeSnapshot(DefId, isActive: true, startedAt: FixedNow.AddHours(-60), lifespanHours: null);
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task RemainingBelowThreshold_ReturnsTrue()
    {
        // Started 60h ago, lifespan 72h => remaining 12h. 12 < 24 => true.
        var json = Serialize(new TrackerRemainingCondition(DefId, "<", 24));
        var snapshot = MakeSnapshot(DefId, isActive: true, startedAt: FixedNow.AddHours(-60), lifespanHours: 72);
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task RemainingAboveThreshold_ReturnsFalse()
    {
        // Started 24h ago, lifespan 72h => remaining 48h. 48 < 12 => false.
        var json = Serialize(new TrackerRemainingCondition(DefId, "<", 12));
        var snapshot = MakeSnapshot(DefId, isActive: true, startedAt: FixedNow.AddHours(-24), lifespanHours: 72);
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    private static string Serialize(TrackerRemainingCondition c) =>
        JsonSerializer.Serialize(c, EvaluatorJson.Options);

    private static TrackerSnapshot MakeSnapshot(
        Guid definitionId, bool isActive, DateTimeOffset? startedAt, decimal? lifespanHours) =>
        new(definitionId, Guid.NewGuid(), TrackerCategory.Consumable, TrackerMode.Duration,
            startedAt, null, lifespanHours, isActive);

    private static SensorContext MakeContext(IReadOnlyList<TrackerSnapshot>? trackerSnapshots) => new()
    {
        LatestValue = 100m,
        LatestTimestamp = FixedNow.UtcDateTime,
        TrendRate = 0m,
        LastReadingAt = FixedNow.UtcDateTime,
        TrackerSnapshots = trackerSnapshots
    };
}
