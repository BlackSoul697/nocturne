using FluentAssertions;
using Nocturne.API.Services.Loopalyzer;
using Xunit;

namespace Nocturne.API.Tests.Services.Loopalyzer;

public class BinInterpolatorTests
{
    [Fact]
    public void RisingShortGap_FillsLinearly()
    {
        var bins = Build(1.0, null, null, null, null, 1.5);

        BinInterpolator.Interpolate(bins, risingGap: 6, fallingGap: 24, ratio: 1.25);

        bins[0].Should().Be(1.0);
        bins[1].Should().BeApproximately(1.1, 1e-9);
        bins[2].Should().BeApproximately(1.2, 1e-9);
        bins[3].Should().BeApproximately(1.3, 1e-9);
        bins[4].Should().BeApproximately(1.4, 1e-9);
        bins[5].Should().Be(1.5);
    }

    [Fact]
    public void RisingLongGap_AllowedWhenRatioWithinThreshold()
    {
        // gap=8 (over rising 6) but end/start=1.10 ≤ 1.25 → allowed.
        var bins = new double?[10];
        bins[0] = 1.0;
        bins[9] = 1.10;

        BinInterpolator.Interpolate(bins, risingGap: 6, fallingGap: 24, ratio: 1.25);

        bins[1].Should().NotBeNull();
        bins[8].Should().NotBeNull();
        bins[5].Should().BeApproximately(1.0 + 0.10 * 5 / 9, 1e-9);
    }

    [Fact]
    public void RisingLongGap_LeavesNullsWhenRatioTooHigh()
    {
        // gap=8 (over rising 6), end/start=1.50 > 1.25 → not allowed.
        var bins = new double?[10];
        bins[0] = 1.0;
        bins[9] = 1.5;

        BinInterpolator.Interpolate(bins, risingGap: 6, fallingGap: 24, ratio: 1.25);

        for (var i = 1; i <= 8; i++)
            bins[i].Should().BeNull();
    }

    [Fact]
    public void FallingGap_FillsWithinWideCap()
    {
        var bins = new double?[22];
        bins[0] = 5.0;
        bins[21] = 4.0;

        BinInterpolator.Interpolate(bins, risingGap: 6, fallingGap: 24, ratio: 1.25);

        bins[10].Should().NotBeNull();
        bins[10]!.Value.Should().BeLessThan(5.0).And.BeGreaterThan(4.0);
    }

    [Fact]
    public void FallingGap_LeavesNullsBeyondWideCap()
    {
        var bins = new double?[32];
        bins[0] = 5.0;
        bins[31] = 4.0;

        BinInterpolator.Interpolate(bins, risingGap: 6, fallingGap: 24, ratio: 1.25);

        bins[15].Should().BeNull();
    }

    [Fact]
    public void LeadingAndTrailingNulls_LeftAlone()
    {
        var bins = new double?[10];
        bins[3] = 1.0;
        bins[5] = 1.2;

        BinInterpolator.Interpolate(bins, risingGap: 6, fallingGap: 24, ratio: 1.25);

        bins[0].Should().BeNull();
        bins[2].Should().BeNull();
        bins[4].Should().BeApproximately(1.1, 1e-9); // gap of 1, rising
        bins[6].Should().BeNull();
        bins[9].Should().BeNull();
    }

    private static double?[] Build(params double?[] values) => values;
}
