using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Nocturne.API.Helpers;
using Xunit;

namespace Nocturne.API.Tests.Helpers;

/// <summary>
/// The Compose and Portainer bundles set <c>BASE_DOMAIN</c> and nothing else, so a deployment
/// that never heard of the <c>BaseUrl</c> key still has to produce a usable public origin —
/// otherwise OIDC redirect URIs point an identity provider at <c>http://localhost:5000</c>.
/// </summary>
public class PublicBaseUrlTests
{
    private static IConfiguration Config(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.ToDictionary(e => e.Key, e => (string?)e.Value))
            .Build();

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WithOnlyBaseDomain_PromotesItToAnHttpsOrigin()
    {
        PublicBaseUrl.Resolve(Config(("BASE_DOMAIN", "example.com")))
            .Should().Be("https://example.com");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WithBaseDomainCarryingAPort_KeepsThePort()
    {
        PublicBaseUrl.Resolve(Config(("BASE_DOMAIN", "localhost:1612")))
            .Should().Be("https://localhost:1612");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WithAnExplicitScheme_DoesNotForceHttps()
    {
        PublicBaseUrl.Resolve(Config(("BASE_DOMAIN", "http://box.lan:8080")))
            .Should().Be("http://box.lan:8080");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_PrefersBaseUrlOverBaseDomain()
    {
        var config = Config(
            ("BaseUrl", "https://nocturne.example.com/"),
            ("BASE_DOMAIN", "example.com")
        );

        PublicBaseUrl.Resolve(config).Should().Be("https://nocturne.example.com");
    }

    /// <summary>
    /// <c>Oidc:BaseUrl</c> is the key operators reach for first, since it sits next to the rest
    /// of the OIDC settings. It read as dead configuration before this resolver.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_HonoursTheOidcSectionKey()
    {
        PublicBaseUrl.Resolve(Config(("Oidc:BaseUrl", "https://sso.example.com")))
            .Should().Be("https://sso.example.com");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WithNothingConfigured_ReturnsNull()
    {
        PublicBaseUrl.Resolve(Config()).Should().BeNull();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("https://example.com/dashboard")]
    [InlineData("https://alice.example.com/dashboard")]
    [InlineData("http://example.com/dashboard")]
    public void BelongsToDeployment_AcceptsTheApexAndItsTenantSubdomains(string url)
    {
        PublicBaseUrl.BelongsToDeployment(Config(("BASE_DOMAIN", "example.com")), new Uri(url))
            .Should().BeTrue();
    }

    /// <summary>
    /// The reason this compares hosts rather than string prefixes: an attacker-controlled
    /// <c>example.com.attacker.test</c> starts with the configured origin.
    /// </summary>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("https://example.com.attacker.test/steal")]
    [InlineData("https://notexample.com/steal")]
    [InlineData("javascript:alert(1)")]
    public void BelongsToDeployment_RejectsLookalikesAndNonHttpSchemes(string url)
    {
        PublicBaseUrl.BelongsToDeployment(Config(("BASE_DOMAIN", "example.com")), new Uri(url))
            .Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BelongsToDeployment_WithNothingConfigured_ReturnsFalse()
    {
        PublicBaseUrl.BelongsToDeployment(Config(), new Uri("https://example.com/"))
            .Should().BeFalse();
    }
}
