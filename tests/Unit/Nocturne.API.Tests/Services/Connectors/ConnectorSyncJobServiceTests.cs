using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nocturne.API.Services.Connectors;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Core.Contracts.Multitenancy;
using Xunit;

namespace Nocturne.API.Tests.Services.Connectors;

/// <summary>
/// Lifecycle behaviour of <see cref="ConnectorSyncJobService"/>: it seeds per-connector progress
/// before any work runs, drives the background sync to a terminal state reflecting per-connector
/// outcomes, propagates the owning tenant's context into each background scope, scopes lookups by
/// tenant, and is idempotent per tenant while a job is active. The per-connector sync service is
/// mocked so these tests are deterministic and need no database.
/// </summary>
public class ConnectorSyncJobServiceTests
{
    private static readonly TenantContext Tenant =
        new(Guid.CreateVersion7(), "erik", "Erik", IsActive: true);

    /// <summary>
    /// Builds a job service whose background scopes resolve the supplied sync-service mock and a
    /// tenant accessor that records the contexts set on it.
    /// </summary>
    private static ConnectorSyncJobService BuildService(
        IConnectorSyncService syncService,
        List<TenantContext>? tenantsSet = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => syncService);
        services.AddScoped<ITenantAccessor>(_ =>
        {
            var accessor = new Mock<ITenantAccessor>();
            if (tenantsSet is not null)
            {
                accessor.Setup(a => a.SetTenant(It.IsAny<TenantContext>()))
                    .Callback<TenantContext>(tenantsSet.Add);
            }
            return accessor.Object;
        });
        var provider = services.BuildServiceProvider();
        return new ConnectorSyncJobService(
            NullLogger<ConnectorSyncJobService>.Instance, provider);
    }

    private static async Task<ConnectorSyncJobStatus> WaitForTerminalAsync(
        ConnectorSyncJobService service, Guid jobId, Guid tenantId)
    {
        for (var i = 0; i < 100; i++)
        {
            var status = service.GetStatus(jobId, tenantId);
            status.Should().NotBeNull();
            if (status!.State is ConnectorSyncJobState.Completed
                or ConnectorSyncJobState.Failed
                or ConnectorSyncJobState.Cancelled)
            {
                return status;
            }
            await Task.Delay(20);
        }

        throw new TimeoutException("Sync job did not reach a terminal state in time.");
    }

    [Fact]
    public void StartSync_SeedsEveryConnectorAsPending()
    {
        var syncService = new Mock<IConnectorSyncService>();
        // Never completes, so the status we read is the seeded one.
        syncService.Setup(s => s.TriggerSyncAsync(
                It.IsAny<string>(), It.IsAny<SyncRequest>(), It.IsAny<CancellationToken>(), It.IsAny<ISyncProgressReporter?>()))
            .Returns(new TaskCompletionSource<SyncResult>().Task);

        var service = BuildService(syncService.Object);

        var status = service.StartSync(Tenant, ["nightscout", "dexcom"], new SyncRequest());

        status.TotalConnectors.Should().Be(2);
        status.CompletedConnectors.Should().Be(0);
        status.Connectors.Select(c => c.ConnectorId).Should().ContainInOrder("nightscout", "dexcom");
        // The background task may already have marked the first connector Running, but none can
        // have finished because the mocked sync never completes.
        status.Connectors.Should().OnlyContain(c =>
            c.State == ConnectorSyncJobConnectorState.Pending
            || c.State == ConnectorSyncJobConnectorState.Running);
    }

    [Fact]
    public async Task StartSync_RunsToCompletion_ReflectingPerConnectorOutcomes()
    {
        var tenantsSet = new List<TenantContext>();
        var syncService = new Mock<IConnectorSyncService>();
        syncService.Setup(s => s.TriggerSyncAsync(
                "nightscout", It.IsAny<SyncRequest>(), It.IsAny<CancellationToken>(), It.IsAny<ISyncProgressReporter?>()))
            .ReturnsAsync(new SyncResult { Success = true, Message = "ok" });
        syncService.Setup(s => s.TriggerSyncAsync(
                "dexcom", It.IsAny<SyncRequest>(), It.IsAny<CancellationToken>(), It.IsAny<ISyncProgressReporter?>()))
            .ReturnsAsync(new SyncResult { Success = false, Message = "nope" });

        var service = BuildService(syncService.Object, tenantsSet);

        var started = service.StartSync(Tenant, ["nightscout", "dexcom"], new SyncRequest());
        var status = await WaitForTerminalAsync(service, started.JobId, Tenant.TenantId);

        status.State.Should().Be(ConnectorSyncJobState.Completed);
        status.CompletedConnectors.Should().Be(2);
        status.Connectors.Should().Contain(c =>
            c.ConnectorId == "nightscout" && c.State == ConnectorSyncJobConnectorState.Succeeded);
        status.Connectors.Should().Contain(c =>
            c.ConnectorId == "dexcom" && c.State == ConnectorSyncJobConnectorState.Failed && c.Message == "nope");
        tenantsSet.Should().OnlyContain(t => t == Tenant,
            "every background scope must carry the owning tenant's context");
        tenantsSet.Should().HaveCount(2, "one scope is created per connector");
    }

    [Fact]
    public void StartSync_WhileJobActive_ReturnsExistingJob()
    {
        var syncService = new Mock<IConnectorSyncService>();
        syncService.Setup(s => s.TriggerSyncAsync(
                It.IsAny<string>(), It.IsAny<SyncRequest>(), It.IsAny<CancellationToken>(), It.IsAny<ISyncProgressReporter?>()))
            .Returns(new TaskCompletionSource<SyncResult>().Task);

        var service = BuildService(syncService.Object);

        var first = service.StartSync(Tenant, ["nightscout"], new SyncRequest());
        var second = service.StartSync(Tenant, ["dexcom"], new SyncRequest());

        second.JobId.Should().Be(first.JobId, "starting is idempotent per tenant while a job is active");
    }

    [Fact]
    public void StartSync_WhileOtherTenantJobActive_StartsIndependentJob()
    {
        var syncService = new Mock<IConnectorSyncService>();
        syncService.Setup(s => s.TriggerSyncAsync(
                It.IsAny<string>(), It.IsAny<SyncRequest>(), It.IsAny<CancellationToken>(), It.IsAny<ISyncProgressReporter?>()))
            .Returns(new TaskCompletionSource<SyncResult>().Task);

        var service = BuildService(syncService.Object);
        var otherTenant = new TenantContext(Guid.CreateVersion7(), "other", "Other", IsActive: true);

        var first = service.StartSync(Tenant, ["nightscout"], new SyncRequest());
        var second = service.StartSync(otherTenant, ["nightscout"], new SyncRequest());

        second.JobId.Should().NotBe(first.JobId);
    }

    [Fact]
    public void GetStatus_UnknownJobOrWrongTenant_ReturnsNull()
    {
        var syncService = new Mock<IConnectorSyncService>();
        syncService.Setup(s => s.TriggerSyncAsync(
                It.IsAny<string>(), It.IsAny<SyncRequest>(), It.IsAny<CancellationToken>(), It.IsAny<ISyncProgressReporter?>()))
            .Returns(new TaskCompletionSource<SyncResult>().Task);

        var service = BuildService(syncService.Object);
        var started = service.StartSync(Tenant, ["nightscout"], new SyncRequest());

        service.GetStatus(Guid.CreateVersion7(), Tenant.TenantId).Should().BeNull();
        service.GetStatus(started.JobId, Guid.CreateVersion7()).Should().BeNull(
            "a job must not be visible to another tenant");
    }

    [Fact]
    public async Task Cancel_ActiveJob_ReachesCancelledState()
    {
        var syncService = new Mock<IConnectorSyncService>();
        syncService.Setup(s => s.TriggerSyncAsync(
                It.IsAny<string>(), It.IsAny<SyncRequest>(), It.IsAny<CancellationToken>(), It.IsAny<ISyncProgressReporter?>()))
            .Returns<string, SyncRequest, CancellationToken, ISyncProgressReporter?>(async (_, _, ct, _) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new SyncResult { Success = true };
            });

        var service = BuildService(syncService.Object);
        var started = service.StartSync(Tenant, ["nightscout", "dexcom"], new SyncRequest());

        service.Cancel(started.JobId, Tenant.TenantId).Should().BeTrue();

        var status = await WaitForTerminalAsync(service, started.JobId, Tenant.TenantId);
        status.State.Should().Be(ConnectorSyncJobState.Cancelled);
    }

    [Fact]
    public void Cancel_UnknownJobOrWrongTenant_ReturnsFalse()
    {
        var syncService = new Mock<IConnectorSyncService>();
        syncService.Setup(s => s.TriggerSyncAsync(
                It.IsAny<string>(), It.IsAny<SyncRequest>(), It.IsAny<CancellationToken>(), It.IsAny<ISyncProgressReporter?>()))
            .Returns(new TaskCompletionSource<SyncResult>().Task);

        var service = BuildService(syncService.Object);
        var started = service.StartSync(Tenant, ["nightscout"], new SyncRequest());

        service.Cancel(Guid.CreateVersion7(), Tenant.TenantId).Should().BeFalse();
        service.Cancel(started.JobId, Guid.CreateVersion7()).Should().BeFalse(
            "a job must not be cancellable by another tenant");
    }
}
