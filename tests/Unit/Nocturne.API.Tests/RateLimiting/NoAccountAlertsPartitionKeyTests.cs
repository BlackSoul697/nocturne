using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Nocturne.API.Extensions;
using Nocturne.Core.Models.Authorization;
using Xunit;

namespace Nocturne.API.Tests.RateLimiting;

[Trait("Category", "Unit")]
public class NoAccountAlertsPartitionKeyTests
{
    [Fact]
    public void EachClockFaceOfAHost_HasItsOwnWindow()
    {
        Key("Kitchen.Example.com", id: "a").Should().Be(Key("kitchen.example.com", id: "a"));
        Key("kitchen.example.com", id: "a").Should().NotBe(Key("kitchen.example.com", id: "b"));
    }

    [Fact]
    public void ACredential_OutranksTheRoute()
    {
        var grant = Guid.NewGuid();

        Key("t.example.com", id: "a", auth: new AuthContext { TokenId = grant })
            .Should().Be(Key("t.example.com", id: "b", auth: new AuthContext { TokenId = grant }));
    }

    [Fact]
    public void TheClientAddress_IsNotPartOfTheKey()
    {
        var first = Context("t.example.com");
        first.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");
        var second = Context("t.example.com");
        second.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.2");

        ServiceRegistrationExtensions.NoAccountAlertsPartitionKey(first)
            .Should().Be(ServiceRegistrationExtensions.NoAccountAlertsPartitionKey(second));
    }

    private static string Key(string host, string? id = null, AuthContext? auth = null)
    {
        var context = Context(host);
        if (id is not null)
            context.Request.RouteValues["id"] = id;
        if (auth is not null)
            context.SetAuthContext(auth);
        return ServiceRegistrationExtensions.NoAccountAlertsPartitionKey(context);
    }

    private static DefaultHttpContext Context(string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        return context;
    }
}
