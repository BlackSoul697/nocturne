using Nocturne.Core.Clinical.LoopAnalysis;

namespace Nocturne.Core.Clinical.Tests.LoopAnalysis;

public class LoopAnalysisRangesTests
{
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    private static DateTime Utc(int y, int mo, int d, int h) => new(y, mo, d, h, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NinetyLocalDaysAcrossFallBack_AreKeptWhole()
    {
        // 2024-04-07 in Sydney is 25 hours long, so these 90 days are 90 days and an hour.
        var range = LoopAnalysisRanges.Resolve(new DateOnly(2024, 3, 1), new DateOnly(2024, 5, 29), Sydney, 90);

        range.Shortened.Should().BeFalse();
        range.From.Should().Be(Utc(2024, 2, 29, 13));
        range.To.Should().Be(Utc(2024, 5, 29, 14));
        (range.To - range.From).Should().Be(TimeSpan.FromDays(90) + TimeSpan.FromHours(1));
    }

    [Fact]
    public void ALongerRange_KeepsItsMostRecentDays_AndPreviousFollowsTheKeptLength()
    {
        var range = LoopAnalysisRanges.Resolve(new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30), Sydney, 90);

        range.Shortened.Should().BeTrue();
        range.StartDate.Should().Be(new DateOnly(2024, 4, 2));
        range.EndDate.Should().Be(new DateOnly(2024, 6, 30));

        var previous = LoopAnalysisRanges.Previous(range, Sydney);
        previous.StartDate.Should().Be(new DateOnly(2024, 1, 3));
        previous.EndDate.Should().Be(new DateOnly(2024, 4, 1));
        previous.To.Should().Be(range.From);
        previous.Shortened.Should().BeFalse();
    }

    [Fact]
    public void Clamped_IsTheLastWindowBeforeNow()
    {
        var now = Utc(2024, 6, 10, 3);

        var range = LoopAnalysisRanges.Clamped(now, TimeSpan.FromHours(24), Sydney);

        range.From.Should().Be(now.AddHours(-24));
        range.To.Should().Be(now);
        range.StartDate.Should().Be(new DateOnly(2024, 6, 9));
        range.EndDate.Should().Be(new DateOnly(2024, 6, 10));
        range.To.Should().BeAfter(range.From);
    }
}
