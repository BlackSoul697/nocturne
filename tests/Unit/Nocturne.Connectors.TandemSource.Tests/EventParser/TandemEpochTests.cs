using FluentAssertions;
using Nocturne.Connectors.TandemSource.EventParser;
using Xunit;

namespace Nocturne.Connectors.TandemSource.Tests.EventParser;

public class TandemEpochTests
{
    [Fact]
    public void EpochDateTimeOffset_Is2008Jan1()
    {
        TandemEpoch.EpochDateTimeOffset.Year.Should().Be(2008);
        TandemEpoch.EpochDateTimeOffset.Month.Should().Be(1);
        TandemEpoch.EpochDateTimeOffset.Day.Should().Be(1);
        TandemEpoch.EpochDateTimeOffset.Hour.Should().Be(0);
        TandemEpoch.EpochDateTimeOffset.Minute.Should().Be(0);
    }

    [Fact]
    public void ToUtcDateTime_ZeroTimestamp_ReturnsEpoch()
    {
        var result = TandemEpoch.ToUtcDateTime(0, TimeZoneInfo.Utc);
        result.Should().Be(new DateTime(2008, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToUtcDateTime_WithTimezone_ConvertsCorrectly()
    {
        var eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        // rawTimestamp=0 means wall-clock 2008-01-01T00:00:00 in Eastern (EST = UTC-5)
        var result = TandemEpoch.ToUtcDateTime(0, eastern);
        result.Should().Be(new DateTime(2008, 1, 1, 5, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToUtcDateTime_LargeTimestamp_ComputesCorrectDate()
    {
        // 2008 is a leap year, so 366 days * 24 * 3600 = 31622400 seconds = 2009-01-01 00:00:00 wall clock
        var result = TandemEpoch.ToUtcDateTime(31_622_400, TimeZoneInfo.Utc);
        result.Year.Should().Be(2009);
        result.Month.Should().Be(1);
        result.Day.Should().Be(1);
    }
}
