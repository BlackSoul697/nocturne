using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Nocturne.API.Services.Alerts.Evaluators;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;
using Xunit;

namespace Nocturne.API.Tests.Services.Alerts.Evaluators;

[Trait("Category", "Unit")]
public class TrackerTimeUntilScheduledEvaluatorTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid DefId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private readonly TrackerTimeUntilScheduledEvaluator _sut;

    public TrackerTimeUntilScheduledEvaluatorTests()
    {
        var timeProvider = new FakeTimeProvider(FixedNow);
        _sut = new TrackerTimeUntilScheduledEvaluator(timeProvider);
    }

    [Fact]
    public void ConditionType_ShouldBeTrackerTimeUntilScheduled()
    {
        _sut.ConditionType.Should().Be(AlertConditionType.TrackerTimeUntilScheduled);
    }

    [Fact]
    public async Task NullTrackerSnapshots_ReturnsFalse()
    {
        var json = Serialize(new TrackerTimeUntilScheduledCondition(DefId, "<", 2));
        var context = MakeContext(trackerSnapshots: null);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task NoMatchingSnapshot_ReturnsFalse()
    {
        var json = Serialize(new TrackerTimeUntilScheduledCondition(DefId, "<", 2));
        var snapshot = MakeSnapshot(Guid.NewGuid(), isActive: true, scheduledAt: FixedNow.AddHours(1));
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task InactiveTracker_ReturnsFalse()
    {
        var json = Serialize(new TrackerTimeUntilScheduledCondition(DefId, "<", 2));
        var snapshot = MakeSnapshot(DefId, isActive: false, scheduledAt: FixedNow.AddHours(1));
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task NullScheduledAt_ReturnsFalse()
    {
        var json = Serialize(new TrackerTimeUntilScheduledCondition(DefId, "<", 2));
        var snapshot = MakeSnapshot(DefId, isActive: true, scheduledAt: null);
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task FutureSchedule_LessThanThreshold_ReturnsTrue()
    {
        // Scheduled 1h from now. 1 < 2 => true.
        var json = Serialize(new TrackerTimeUntilScheduledCondition(DefId, "<", 2));
        var snapshot = MakeSnapshot(DefId, isActive: true, scheduledAt: FixedNow.AddHours(1));
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task FutureSchedule_AboveThreshold_ReturnsFalse()
    {
        // Scheduled 5h from now. 5 < 2 => false.
        var json = Serialize(new TrackerTimeUntilScheduledCondition(DefId, "<", 2));
        var snapshot = MakeSnapshot(DefId, isActive: true, scheduledAt: FixedNow.AddHours(5));
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task PastDueSchedule_NegativeHours_ReturnsTrue()
    {
        // Scheduled 3h ago => hoursUntil = -3. -3 < 0 => true.
        var json = Serialize(new TrackerTimeUntilScheduledCondition(DefId, "<", 0));
        var snapshot = MakeSnapshot(DefId, isActive: true, scheduledAt: FixedNow.AddHours(-3));
        var context = MakeContext(trackerSnapshots: [snapshot]);

        (await _sut.EvaluateAsync(json, context, CancellationToken.None)).Should().BeTrue();
    }

    private static string Serialize(TrackerTimeUntilScheduledCondition c) =>
        JsonSerializer.Serialize(c, EvaluatorJson.Options);

    private static TrackerSnapshot MakeSnapshot(
        Guid definitionId, bool isActive, DateTimeOffset? scheduledAt) =>
        new(definitionId, Guid.NewGuid(), TrackerCategory.Consumable, TrackerMode.Event,
            FixedNow.AddHours(-24), scheduledAt, null, isActive);

    private static SensorContext MakeContext(IReadOnlyList<TrackerSnapshot>? trackerSnapshots) => new()
    {
        LatestValue = 100m,
        LatestTimestamp = FixedNow.UtcDateTime,
        TrendRate = 0m,
        LastReadingAt = FixedNow.UtcDateTime,
        TrackerSnapshots = trackerSnapshots
    };
}
