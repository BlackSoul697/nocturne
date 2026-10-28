using System.Net;
using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Nocturne.Aspire.Host.Tests;

public class DevTenantSeedResourceTests
{
    [Fact]
    public async Task A_failed_seed_still_finishes_with_exit_code_0()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder.AddContainer("api", "api-image").WithHttpEndpoint(name: "http");
        var seed = builder.AddDevTenantSeed("dev-seed", api, sampleDataDays: 30);

        // Nothing listens on the port, so the seed fails to connect.
        var endpoint = api.Resource.Annotations.OfType<EndpointAnnotation>().Single();
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", UnusedPort());
        // The wait on the API is not under test.
        seed.Resource.Annotations.Remove(seed.Resource.Annotations.OfType<WaitAnnotation>().Single());

        await using var app = builder.Build();
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        var eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await eventing.PublishAsync(
            new InitializeResourceEvent(
                seed.Resource,
                eventing,
                app.Services.GetRequiredService<ResourceLoggerService>(),
                notifications,
                app.Services),
            timeout.Token);

        var finished = await notifications.WaitForResourceAsync(
            seed.Resource.Name,
            e => KnownResourceStates.TerminalStates.Contains(e.Snapshot.State?.Text),
            timeout.Token);
        finished.Snapshot.State?.Text.Should().Be(KnownResourceStates.Finished);
        finished.Snapshot.ExitCode.Should().Be(0);
    }

    [Fact]
    public void The_seed_waits_for_the_api()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder.AddContainer("api", "api-image").WithHttpEndpoint(name: "http");

        var seed = builder.AddDevTenantSeed("dev-seed", api, sampleDataDays: 30);

        seed.Resource.Annotations.OfType<WaitAnnotation>().Should().ContainSingle(w =>
            w.Resource == api.Resource && w.WaitType == WaitType.WaitUntilHealthy);
    }

    private static int UnusedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
