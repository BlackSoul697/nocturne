using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nocturne.API.Extensions;
using Nocturne.Core.Models.Configuration;

namespace Nocturne.API.Tests.Extensions;

/// <summary>
/// The translation-drafts limiter partitions before AuthenticationMiddleware
/// runs, so the key can only come from the credential the request carries —
/// never from a header the caller can rotate for free.
/// </summary>
public class TranslationDraftPartitionKeyTests
{
    private static readonly OidcOptions Options = new();

    private static DefaultHttpContext Context(
        string? accessToken = null,
        string? refreshToken = null,
        string? authorization = null,
        string? forwardedFor = null,
        string remoteIp = "10.0.0.1")
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<OidcOptions>>(new OptionsWrapper<OidcOptions>(Options));

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);

        var cookies = new List<string>();
        if (accessToken is not null)
            cookies.Add($"{Options.Cookie.AccessTokenName}={accessToken}");
        if (refreshToken is not null)
            cookies.Add($"{Options.Cookie.RefreshTokenName}={refreshToken}");
        if (cookies.Count > 0)
            context.Request.Headers.Cookie = string.Join("; ", cookies);
        if (authorization is not null)
            context.Request.Headers.Authorization = authorization;
        if (forwardedFor is not null)
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;

        return context;
    }

    private static string Key(DefaultHttpContext context) =>
        ServiceRegistrationExtensions.TranslationDraftPartitionKey(context);

    [Fact]
    public void Session_Cookie_Gets_Its_Own_Partition()
    {
        var mine = Key(Context(accessToken: "jwt-a"));
        var theirs = Key(Context(accessToken: "jwt-b"));

        mine.Should().NotBe(theirs);
        mine.Should().Be(Key(Context(accessToken: "jwt-a")));
        mine.Should().NotBe(ServiceRegistrationExtensions.AnonymousDraftPartition);
    }

    [Fact]
    public void Bearer_Token_Gets_Its_Own_Partition()
    {
        var mine = Key(Context(authorization: "Bearer jwt-a"));

        mine.Should().NotBe(Key(Context(authorization: "Bearer jwt-b")));
        mine.Should().NotBe(ServiceRegistrationExtensions.AnonymousDraftPartition);
    }

    [Fact]
    public void Refresh_Token_Carries_The_Partition_When_The_Access_Token_Expired()
    {
        // SessionCookieHandler falls back to the refresh token, so a request
        // that only carries one must still land in a per-session bucket.
        Key(Context(refreshToken: "refresh-a"))
            .Should().NotBe(ServiceRegistrationExtensions.AnonymousDraftPartition);
        Key(Context(refreshToken: "refresh-a"))
            .Should().NotBe(Key(Context(refreshToken: "refresh-b")));
    }

    [Fact]
    public void Credential_Is_Never_The_Key_Itself()
    {
        Key(Context(accessToken: "secret-token")).Should().NotContain("secret-token");
    }

    [Fact]
    public void Key_Does_Not_Derive_From_Caller_Controlled_Headers()
    {
        // Rotating X-Forwarded-For (which UseForwardedHeaders turns into
        // RemoteIpAddress) must not mint a fresh bucket for the same session.
        var first = Key(Context(accessToken: "jwt-a", forwardedFor: "1.2.3.4", remoteIp: "1.2.3.4"));
        var second = Key(Context(accessToken: "jwt-a", forwardedFor: "5.6.7.8", remoteIp: "5.6.7.8"));

        first.Should().Be(second);
    }

    [Fact]
    public void Credentialless_Requests_Share_One_Fixed_Bucket()
    {
        // No per-IP fallback: an anonymous flood rotating X-Forwarded-For gets
        // the same bucket every time rather than an unlimited supply of them.
        Key(Context(forwardedFor: "1.2.3.4", remoteIp: "1.2.3.4"))
            .Should().Be(ServiceRegistrationExtensions.AnonymousDraftPartition);
        Key(Context(forwardedFor: "5.6.7.8", remoteIp: "5.6.7.8"))
            .Should().Be(ServiceRegistrationExtensions.AnonymousDraftPartition);
    }
}
