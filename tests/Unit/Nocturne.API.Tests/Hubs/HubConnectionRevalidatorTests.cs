using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nocturne.API.Extensions;
using Nocturne.API.Hubs;
using Nocturne.API.Multitenancy;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;

namespace Nocturne.API.Tests.Hubs;

/// <summary>
/// A hub connection keeps its access only while its credential, proven again from the same source,
/// still covers what it connected with. A removed member, a revoked grant, an expired token, a
/// narrowed scope or a new history clamp closes the connection within one sweep.
/// </summary>
[Trait("Category", "Unit")]
public class HubConnectionRevalidatorTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Subject = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly TenantContext TenantContext =
        new(Tenant, "default", "Default", IsActive: true, IsDemo: false);

    private sealed class TestHttpContextFeature : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; }
    }

    private readonly HubConnectionRevalidator _revalidator = new(
        new ServiceCollection()
            .AddScoped<ITenantAccessor, HttpContextTenantAccessor>()
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>(),
        TimeProvider.System,
        NullLogger<HubConnectionRevalidator>.Instance);

    private static HubAuthorization Member(bool historyClamped = false, params string[] scopes) => new(
        Tenant, Scope.Normalize(scopes), HubCredentialKind.Subject, Subject, historyClamped);

    private static Mock<HubCallerContext> Connection(string connectionId = "conn-1")
    {
        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        httpContext.SetTenantContext(TenantContext);
        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new TestHttpContextFeature { HttpContext = httpContext });

        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns(connectionId);
        context.SetupGet(c => c.Features).Returns(features);
        context.SetupGet(c => c.Items).Returns(new Dictionary<object, object?>());
        return context;
    }

    private Mock<HubCallerContext> Tracked(HubAuthorization granted, HubCredentialCheck recheck)
    {
        var connection = Connection();
        HubAuthorizationState.Grant(connection.Object, granted, recheck);
        _revalidator.Track(connection.Object);
        return connection;
    }

    private static HubCredentialCheck Proves(HubAuthorization? current) =>
        (_, _) => Task.FromResult(current);

    public static TheoryData<string, HubAuthorization?> Withdrawn => new()
    {
        { "refused", null },
        { "narrowed", Member(false, Scope.GlucoseRead) },
        { "newly clamped", Member(true, Scope.GlucoseRead, Scope.TreatmentsRead) },
        { "another subject", Member(false, Scope.GlucoseRead, Scope.TreatmentsRead) with { SubjectId = Guid.NewGuid() } },
        { "another kind", Member(false, Scope.GlucoseRead, Scope.TreatmentsRead) with { Kind = HubCredentialKind.Restricted } },
        { "another client", Member(false, Scope.GlucoseRead, Scope.TreatmentsRead) with { ClientId = "other" } },
    };

    [Theory]
    [MemberData(nameof(Withdrawn))]
    public async Task A_connection_whose_credential_no_longer_covers_it_is_closed(
        string because, HubAuthorization? current)
    {
        var connection = Tracked(Member(false, Scope.GlucoseRead, Scope.TreatmentsRead), Proves(current));

        await _revalidator.RevalidateAsync(CancellationToken.None);

        connection.Verify(c => c.Abort(), Times.Once, because);
    }

    public static TheoryData<string, HubAuthorization, HubAuthorization> StillCovered => new()
    {
        { "unchanged", Member(false, Scope.GlucoseRead), Member(false, Scope.GlucoseRead) },
        { "widened", Member(false, Scope.GlucoseRead), Member(false, Scope.GlucoseRead, Scope.DevicesRead) },
        { "clamp lifted", Member(true, Scope.GlucoseRead), Member(false, Scope.GlucoseRead) },
    };

    [Theory]
    [MemberData(nameof(StillCovered))]
    public async Task A_connection_whose_credential_still_covers_it_stays_open(
        string because, HubAuthorization granted, HubAuthorization current)
    {
        var connection = Tracked(granted, Proves(current));

        await _revalidator.RevalidateAsync(CancellationToken.None);

        connection.Verify(c => c.Abort(), Times.Never, because);
    }

    [Fact]
    public async Task A_closed_connection_is_checked_no_further()
    {
        var checks = 0;
        Tracked(Member(false, Scope.GlucoseRead), (_, _) =>
        {
            checks++;
            return Task.FromResult<HubAuthorization?>(null);
        });

        await _revalidator.RevalidateAsync(CancellationToken.None);
        await _revalidator.RevalidateAsync(CancellationToken.None);

        checks.Should().Be(1);
    }

    [Fact]
    public async Task A_check_that_cannot_answer_keeps_the_connection_until_the_next_sweep()
    {
        var connection = Tracked(Member(false, Scope.GlucoseRead), (_, _) => throw new InvalidOperationException());

        await _revalidator.RevalidateAsync(CancellationToken.None);

        connection.Verify(c => c.Abort(), Times.Never);
    }

    [Fact]
    public async Task The_check_runs_in_a_scope_pinned_to_the_connections_tenant()
    {
        TenantContext? seen = null;
        Tracked(Member(false, Scope.GlucoseRead), (services, _) =>
        {
            seen = services.GetRequiredService<ITenantAccessor>().Context;
            return Task.FromResult<HubAuthorization?>(Member(false, Scope.GlucoseRead));
        });

        await _revalidator.RevalidateAsync(CancellationToken.None);

        seen.Should().Be(TenantContext);
    }

    private static HubInvocationContext Invocation(HubCallerContext context) => new(
        context,
        Mock.Of<IServiceProvider>(),
        hub: new StubHub(),
        hubMethod: typeof(AlertHub).GetMethod(nameof(AlertHub.Subscribe), BindingFlags.Public | BindingFlags.Instance)!,
        hubMethodArguments: []);

    private sealed class StubHub : Hub;

    [Fact]
    public async Task The_filter_hands_an_authorized_connection_to_the_revalidator_and_takes_it_back_on_disconnect()
    {
        var filter = new HubAuthorizationFilter(_revalidator);
        var connection = Connection();
        HubAuthorizationState.Grant(connection.Object, Member(false, Scope.AlertsRead), Proves(null));

        await filter.InvokeMethodAsync(Invocation(connection.Object), _ => ValueTask.FromResult<object?>(null));
        await _revalidator.RevalidateAsync(CancellationToken.None);

        connection.Verify(c => c.Abort(), Times.Once);

        var disconnected = Connection("conn-2");
        HubAuthorizationState.Grant(disconnected.Object, Member(false, Scope.AlertsRead), Proves(null));
        await filter.InvokeMethodAsync(Invocation(disconnected.Object), _ => ValueTask.FromResult<object?>(null));
        await filter.OnDisconnectedAsync(
            new HubLifetimeContext(disconnected.Object, Mock.Of<IServiceProvider>(), new StubHub()),
            null,
            (_, _) => Task.CompletedTask);
        await _revalidator.RevalidateAsync(CancellationToken.None);

        disconnected.Verify(c => c.Abort(), Times.Never);
    }

    [Fact]
    public void A_handshake_replay_never_carries_the_refresh_token_cookie()
    {
        var header = new Microsoft.Extensions.Primitives.StringValues(
            ".Nocturne.AccessToken=abc; .Nocturne.RefreshToken=def; other=1");

        HandshakeCredential.WithoutCookie(header, ".Nocturne.RefreshToken")
            .Should().Be(".Nocturne.AccessToken=abc; other=1");
    }
}
