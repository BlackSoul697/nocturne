using FluentAssertions;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.Core.Models.Tests.V4;

public class ScheduleEntryTimeTests
{
    [Theory]
    [InlineData("06:00", 21600)]
    [InlineData("6:00", 21600)]
    [InlineData("06:00:00", 21600)]
    [InlineData("23:59:59", 86399)]
    public void ASpelledTimeOfDay_IsTheSameSecondsOnBothPaths(string time, int seconds)
    {
        ScheduleEntryTime.SecondsOf(null, time).Should().Be(seconds);

        var value = new TimeValue { Time = time };
        value.EnsureTimeAsSeconds();
        value.TimeAsSeconds.Should().Be(seconds);
    }

    [Theory]
    [InlineData("6")]
    [InlineData("")]
    [InlineData("not a time")]
    public void ABareNumberOrGarbage_IsNotATimeOfDay_OnEitherPath(string time)
    {
        ScheduleEntryTime.SecondsOf(null, time).Should().Be(0);

        var value = new TimeValue { Time = time };
        value.EnsureTimeAsSeconds();
        value.TimeAsSeconds.Should().BeNull();
    }

    [Fact]
    public void ASuffixedTime_IsRejectedOnBothPaths()
    {
        ScheduleEntryTime.SecondsOf(null, "6:00 AM").Should().Be(0);

        var value = new TimeValue { Time = "6:00 AM" };
        value.EnsureTimeAsSeconds();
        value.TimeAsSeconds.Should().BeNull();
    }

    [Fact]
    public void AStoredTimeAsSeconds_WinsOverTheText()
    {
        ScheduleEntryTime.SecondsOf(3600, "06:00").Should().Be(3600);
    }
}
