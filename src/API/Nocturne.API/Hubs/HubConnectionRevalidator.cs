using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Nocturne.API.Extensions;
using Nocturne.Core.Contracts.Multitenancy;

namespace Nocturne.API.Hubs;

/// <summary>
/// Proves every authorized hub connection's credential again each <see cref="Interval"/>, and closes
/// a connection whose credential no longer covers what it connected with
/// (<see cref="HubAuthorization.IsCoveredBy"/>): a removed member, a revoked grant or guest link, an
/// expired token, narrowed scopes or a new history clamp.
/// </summary>
/// <remarks>
/// A connection is closed rather than narrowed in place because the groups it joined were chosen
/// from the old credential. Closing makes the client reconnect, and its new connection joins only
/// what its current credential allows. Each node re-checks the connections it holds, so this needs
/// no backplane.
/// </remarks>
public sealed class HubConnectionRevalidator(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<HubConnectionRevalidator> logger) : BackgroundService
{
    /// <summary>How long a connection may outlive the withdrawal of its credential.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, HubCallerContext> _connections = new();

    /// <summary>Starts re-checking <paramref name="context"/>'s credential.</summary>
    public void Track(HubCallerContext context) => _connections.TryAdd(context.ConnectionId, context);

    /// <summary>Stops re-checking a connection that has closed.</summary>
    public void Forget(HubCallerContext context) => _connections.TryRemove(context.ConnectionId, out _);

    /// <summary>Re-checks every tracked connection once, closing each that fails.</summary>
    public Task RevalidateAsync(CancellationToken ct) =>
        Parallel.ForEachAsync(
            _connections.Values,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (context, token) =>
            {
                if (await StillCoveredAsync(context, token))
                    return;

                logger.LogInformation(
                    "Closing hub connection {ConnectionId}: its credential no longer authorizes it",
                    context.ConnectionId);
                Forget(context);
                context.Abort();
            });

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RevalidateAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <remarks>
    /// A check that throws keeps the connection until the next sweep: it says nothing about the
    /// credential, and the handlers it replays turn a refused credential into a null, not a throw.
    /// </remarks>
    private async Task<bool> StillCoveredAsync(HubCallerContext context, CancellationToken ct)
    {
        if (HubAuthorizationState.Current(context) is not { } grant)
            return true;

        await using var scope = scopeFactory.CreateAsyncScope();
        if (context.GetHttpContext()?.GetTenantContext() is { } tenant)
            scope.ServiceProvider.GetRequiredService<ITenantAccessor>().SetTenant(tenant);

        try
        {
            return grant.Authorization.IsCoveredBy(await grant.Recheck(scope.ServiceProvider, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not re-check hub connection {ConnectionId}", context.ConnectionId);
            return true;
        }
    }
}
