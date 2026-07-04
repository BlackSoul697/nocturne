using FluentAssertions;
using Nocturne.Connectors.CareLink.Utilities;
using Xunit;

namespace Nocturne.Connectors.CareLink.Tests.Utilities;

public class CareLinkTimezoneResolverTests
{
    [Fact]
    public void ResolveIana_MapsCuratedWindowsDisplayName_ToIana()
    {
        CareLinkTimezoneResolver.ResolveIana("Australian Western Standard Time")
            .Should().Be("Australia/Perth");
    }

    [Fact]
    public void ResolveIana_ResolvesGenuineIanaZone()
    {
        CareLinkTimezoneResolver.ResolveIana("Australia/Perth")
            .Should().Be("Australia/Perth");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Totally Made Up Zone Name")]
    public void ResolveIana_ReturnsNull_ForBlankOrUnknown(string? input)
    {
        CareLinkTimezoneResolver.ResolveIana(input).Should().BeNull();
    }

    [Fact]
    public void ResolveIana_ReturnsNull_ForUtc_NeverSeedsUtc()
    {
        CareLinkTimezoneResolver.ResolveIana("UTC").Should().BeNull();
    }
}
