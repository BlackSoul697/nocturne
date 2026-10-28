using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// A one-shot that runs <see cref="DevTenantSeeder"/> in the AppHost once the API is healthy and
/// then finishes, always with exit code 0, so a resource that waits for its completion starts
/// whether or not the seed went through.
/// </summary>
public sealed class DevTenantSeedResource(string name) : Resource(name), IResourceWithWaitSupport;

public static class DevTenantSeedExtensions
{
    public static IResourceBuilder<DevTenantSeedResource> AddDevTenantSeed(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<IResourceWithEndpoints> api,
        int sampleDataDays)
    {
        return builder
            .AddResource(new DevTenantSeedResource(name))
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "DevTenantSeed",
                State = KnownResourceStates.NotStarted,
                Properties = [],
            })
            .WaitFor(api)
            .ExcludeFromManifest()
            .OnInitializeResource(async (resource, e, ct) =>
            {
                // Releases the WaitFor above, which holds until the API is healthy.
                await e.Eventing.PublishAsync(new BeforeResourceStartedEvent(resource, e.Services), ct);

                await e.Notifications.PublishUpdateAsync(resource, s => s with
                {
                    State = KnownResourceStates.Running,
                    StartTimeStamp = DateTime.UtcNow,
                });

                using var http = new HttpClient
                {
                    BaseAddress = new Uri(api.GetEndpoint("http").Url),
                    Timeout = TenantSeedClient.Timeout,
                };
                await new DevTenantSeeder(http, e.Logger).RunAsync(sampleDataDays, ct);

                await e.Notifications.PublishUpdateAsync(resource, s => s with
                {
                    State = KnownResourceStates.Finished,
                    ExitCode = 0,
                    StopTimeStamp = DateTime.UtcNow,
                });
            });
    }
}
