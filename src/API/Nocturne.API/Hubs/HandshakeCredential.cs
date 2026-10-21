using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Nocturne.API.Extensions;
using Nocturne.API.Middleware;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Configuration;

namespace Nocturne.API.Hubs;

/// <summary>
/// Proves again the credential a hub connection presented on its HTTP upgrade request, by replaying
/// a copy of that request through <see cref="AuthenticationMiddleware"/> and
/// <see cref="MemberScopeMiddleware"/>, so a re-check applies exactly the rules the handshake did.
/// </summary>
/// <remarks>
/// The copy leaves out the refresh-token cookie. The replay's response goes nowhere, so a refresh
/// it triggered would rotate the session's refresh token without the browser ever receiving the
/// new one. A session connection therefore lapses with its access token, and its client reconnects
/// through a request whose response it does receive.
/// </remarks>
internal static class HandshakeCredential
{
    private sealed record Snapshot(
        string Method,
        string Scheme,
        HostString Host,
        PathString PathBase,
        PathString Path,
        QueryString Query,
        KeyValuePair<string, StringValues>[] Headers,
        IPAddress? RemoteIpAddress,
        TenantContext Tenant,
        bool IsShareAccess);

    /// <summary>A check that replays <paramref name="handshake"/>'s credential.</summary>
    /// <param name="handshake">An upgrade request that has passed tenant resolution.</param>
    public static HubCredentialCheck Capture(HttpContext handshake)
    {
        var request = handshake.Request;
        var tenant = handshake.GetTenantContext();
        if (tenant is null)
            return static (_, _) => Task.FromResult<HubAuthorization?>(null);

        var snapshot = new Snapshot(
            request.Method,
            request.Scheme,
            request.Host,
            request.PathBase,
            request.Path,
            request.QueryString,
            [.. request.Headers],
            handshake.Connection.RemoteIpAddress,
            tenant,
            handshake.IsShareAccess());

        return (services, ct) => ReplayAsync(snapshot, services, ct);
    }

    private static async Task<HubAuthorization?> ReplayAsync(
        Snapshot snapshot, IServiceProvider services, CancellationToken ct)
    {
        var refreshCookie = services.GetRequiredService<IOptions<OidcOptions>>().Value.Cookie.RefreshTokenName;

        var replay = new DefaultHttpContext { RequestServices = services, RequestAborted = ct };
        var request = replay.Request;
        request.Method = snapshot.Method;
        request.Scheme = snapshot.Scheme;
        request.Host = snapshot.Host;
        request.PathBase = snapshot.PathBase;
        request.Path = snapshot.Path;
        request.QueryString = snapshot.Query;
        foreach (var (name, value) in snapshot.Headers)
            request.Headers[name] = value;
        request.Headers.Cookie = WithoutCookie(request.Headers.Cookie, refreshCookie);
        replay.Connection.RemoteIpAddress = snapshot.RemoteIpAddress;
        replay.SetTenantContext(snapshot.Tenant);
        if (snapshot.IsShareAccess)
            replay.SetShareAccess();

        HubAuthorization? proven = null;
        var memberScope = ActivatorUtilities.CreateInstance<MemberScopeMiddleware>(
            services,
            (RequestDelegate)(context =>
            {
                proven = HubAuthorizationState.FromRequest(context);
                return Task.CompletedTask;
            }));
        var authentication = ActivatorUtilities.CreateInstance<AuthenticationMiddleware>(
            services, (RequestDelegate)memberScope.InvokeAsync);

        await authentication.InvokeAsync(replay);
        return proven;
    }

    /// <summary>The <c>Cookie</c> header values with every <paramref name="name"/> cookie removed.</summary>
    internal static string WithoutCookie(StringValues header, string name) =>
        string.Join("; ", header
            .SelectMany(value => (value ?? "").Split(';'))
            .Select(pair => pair.Trim())
            .Where(pair => pair.Length > 0
                && !string.Equals(pair.Split('=', 2)[0].Trim(), name, StringComparison.Ordinal)));
}
