using FluentAssertions;
using Nocturne.API.Services.Loopalyzer;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services.Loopalyzer;

public class LoopalyzerBinningTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly DateOnly Day = new(2026, 5, 1);

    private static Entry EntryAt(DateTime utcLocal, double sgv) => new()
    {
        Mills = new DateTimeOffset(DateTime.SpecifyKind(utcLocal, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds(),
        Sgv = sgv,
    };

    [Fact]
    public void BinSgvs_ReturnsLength288_WhenNoEntries()
    {
        var bins = LoopalyzerBinning.BinSgvs(Array.Empty<Entry>(), Day, Utc);

        bins.Should().HaveCount(288);
        bins.Should().OnlyContain(b => b == null);
    }

    [Fact]
    public void BinSgvs_AssignsBinByLocalMinuteOfDay()
    {
        var entry = EntryAt(new DateTime(2026, 5, 1, 1, 7, 30), 120);

        var bins = LoopalyzerBinning.BinSgvs(new[] { entry }, Day, Utc);

        // 01:07:30 -> 67 minutes -> bin 13 (67/5 = 13.4 -> 13)
        bins[13].Should().Be(120);
        bins[12].Should().BeNull();
        bins[14].Should().BeNull();
    }

    [Fact]
    public void BinSgvs_LastEntryWins_WithinSameBin()
    {
        var first = EntryAt(new DateTime(2026, 5, 1, 0, 0, 0), 100);
        var middle = EntryAt(new DateTime(2026, 5, 1, 0, 2, 0), 110);
        var last = EntryAt(new DateTime(2026, 5, 1, 0, 4, 30), 120);

        var bins = LoopalyzerBinning.BinSgvs(new[] { last, first, middle }, Day, Utc);

        bins[0].Should().Be(120);
    }

    [Fact]
    public void BinSgvs_FillsExpectedBinsAcrossHours()
    {
        var entries = new[]
        {
            EntryAt(new DateTime(2026, 5, 1, 0, 0, 0), 100),
            EntryAt(new DateTime(2026, 5, 1, 6, 0, 0), 110),
            EntryAt(new DateTime(2026, 5, 1, 12, 0, 0), 120),
            EntryAt(new DateTime(2026, 5, 1, 23, 55, 0), 130),
        };

        var bins = LoopalyzerBinning.BinSgvs(entries, Day, Utc);

        bins[0].Should().Be(100);
        bins[72].Should().Be(110);
        bins[144].Should().Be(120);
        bins[287].Should().Be(130);
    }

    [Fact]
    public void BinSgvs_ExcludesEntriesOutsideDay()
    {
        var entries = new[]
        {
            EntryAt(new DateTime(2026, 4, 30, 23, 59, 0), 100),
            EntryAt(new DateTime(2026, 5, 2, 0, 0, 0), 200),
            EntryAt(new DateTime(2026, 5, 1, 12, 0, 0), 150),
        };

        var bins = LoopalyzerBinning.BinSgvs(entries, Day, Utc);

        bins[144].Should().Be(150);
        bins.Where(b => b.HasValue).Should().HaveCount(1);
    }

    [Fact]
    public void BinSgvs_FallsBackToMgdl_WhenSgvIsNull()
    {
        var entry = new Entry
        {
            Mills = new DateTimeOffset(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            Mgdl = 95,
        };

        var bins = LoopalyzerBinning.BinSgvs(new[] { entry }, Day, Utc);

        bins[0].Should().Be(95);
    }

    [Fact]
    public void BinSgvs_RespectsTimeZone()
    {
        // UTC 04:00 == NY 00:00 EDT (UTC-4 in May).
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var entry = EntryAt(new DateTime(2026, 5, 1, 4, 0, 0), 100);

        var bins = LoopalyzerBinning.BinSgvs(new[] { entry }, Day, ny);

        bins[0].Should().Be(100);
    }
}
