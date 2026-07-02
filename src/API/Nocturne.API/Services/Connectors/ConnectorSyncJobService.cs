using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Nocturne.Connectors.Core.Models;
using Nocturne.Core.Contracts.Multitenancy;

namespace Nocturne.API.Services.Connectors;

/// <summary>
/// Runs a manual connector sync as a background job so the HTTP request that kicks it off can
/// return immediately (202) instead of blocking for the minutes a multi-connector sync can take
/// (which times out at reverse proxies / CDNs, e.g. Cloudflare 524). Callers poll
/// <see cref="GetStatus"/> for per-connector progress.
/// </summary>
/// <remarks>
/// Modelled on <see cref="IConnectorCursorResetJobService"/>: jobs run on detached
/// <see cref="Task"/> instances tracked in a <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed
/// by job id. Unlike the platform-admin reset jobs, these are started by tenant admins, so every
/// lookup is scoped by the caller's tenant id — a job is invisible to any other tenant. Connectors
/// are synced sequentially (matching the previous synchronous UI behaviour) inside fresh DI scopes
/// with the owning tenant's context applied, so sync progress still streams to the tenant's
/// SignalR group via <see cref="Nocturne.Connectors.Core.Interfaces.ISyncProgressReporter"/>.
/// </remarks>
/// <seealso cref="IConnectorSyncService"/>
public interface IConnectorSyncJobService
{
    /// <summary>
    /// Starts a background sync of the given connectors for the tenant, or returns the tenant's
    /// already-active job if one is still pending/running (starting is idempotent per tenant).
    /// </summary>
    /// <param name="tenant">The tenant the sync runs for; captured because the background task outlives the request scope.</param>
    /// <param name="connectorIds">The connectors to sync, in order.</param>
    /// <param name="request">The sync request (date range and data types) applied to every connector.</param>
    /// <returns>The created (or already-active) job's status snapshot.</returns>
    ConnectorSyncJobStatus StartSync(
        TenantContext tenant,
        IReadOnlyList<string> connectorIds,
        SyncRequest request);

    /// <summary>Returns a snapshot of a job's progress, or null when the job does not exist or belongs to another tenant.</summary>
    ConnectorSyncJobStatus? GetStatus(Guid jobId, Guid tenantId);

    /// <summary>Requests cancellation of a running job. Returns false when the job does not exist or belongs to another tenant.</summary>
    bool Cancel(Guid jobId, Guid tenantId);
}

/// <inheritdoc cref="IConnectorSyncJobService"/>
public class ConnectorSyncJobService : IConnectorSyncJobService
{
    /// <summary>How long finished jobs stay pollable before being pruned.</summary>
    private static readonly TimeSpan CompletedJobRetention = TimeSpan.FromHours(1);

    private readonly ILogger<ConnectorSyncJobService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Guid, ConnectorSyncJob> _jobs = new();

    /// <summary>Initializes a new instance of <see cref="ConnectorSyncJobService"/>.</summary>
    public ConnectorSyncJobService(
        ILogger<ConnectorSyncJobService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public ConnectorSyncJobStatus StartSync(
        TenantContext tenant,
        IReadOnlyList<string> connectorIds,
        SyncRequest request)
    {
        PruneCompletedJobs();

        // One active job per tenant: a second click on "Sync Now" (or a page reload mid-sync)
        // attaches to the running job instead of racing a duplicate sync against it.
        var active = _jobs.Values.FirstOrDefault(j =>
            j.TenantId == tenant.TenantId && !j.IsTerminal);
        if (active is not null)
        {
            _logger.LogInformation(
                "Manual sync requested for tenant {TenantSlug} while job {JobId} is active; returning existing job",
                tenant.Slug, active.JobId);
            return active.GetStatus();
        }

        var job = new ConnectorSyncJob(
            Guid.CreateVersion7(),
            tenant,
            connectorIds,
            request,
            _logger,
            _serviceProvider);
        _jobs[job.JobId] = job;

        // Detached background task: deliberately uses CancellationToken.None, not the request token,
        // so the sync outlives the HTTP request that started it. User-initiated cancellation flows
        // through the job's own CancellationTokenSource via Cancel().
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await job.ExecuteAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Connector sync job {JobId} failed", job.JobId);
                }
            },
            CancellationToken.None);

        _logger.LogInformation(
            "Started connector sync job {JobId} for tenant {TenantSlug} ({ConnectorCount} connectors)",
            job.JobId, tenant.Slug, connectorIds.Count);

        return job.GetStatus();
    }

    /// <inheritdoc />
    public ConnectorSyncJobStatus? GetStatus(Guid jobId, Guid tenantId)
    {
        return _jobs.TryGetValue(jobId, out var job) && job.TenantId == tenantId
            ? job.GetStatus()
            : null;
    }

    /// <inheritdoc />
    public bool Cancel(Guid jobId, Guid tenantId)
    {
        if (_jobs.TryGetValue(jobId, out var job) && job.TenantId == tenantId)
        {
            job.Cancel();
            _logger.LogInformation("Cancelled connector sync job {JobId}", jobId);
            return true;
        }

        return false;
    }

    /// <summary>Drops terminal jobs past the retention window so the dictionary cannot grow unboundedly.</summary>
    private void PruneCompletedJobs()
    {
        var cutoff = DateTime.UtcNow - CompletedJobRetention;
        foreach (var (id, job) in _jobs)
        {
            if (job.IsTerminal && job.CompletedAt is { } completedAt && completedAt < cutoff)
                _jobs.TryRemove(id, out _);
        }
    }
}

/// <summary>
/// A single tenant's manual sync running in the background. Tracks lifecycle state and
/// per-connector progress; connectors run sequentially, each inside a fresh DI scope carrying the
/// owning tenant's context.
/// </summary>
internal sealed class ConnectorSyncJob
{
    private readonly TenantContext _tenant;
    private readonly IReadOnlyList<string> _connectorIds;
    private readonly SyncRequest _request;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly CancellationTokenSource _cts = new();
    private readonly DateTime _createdAt = DateTime.UtcNow;

    // Preserves the requested order; entries are replaced in place as connectors progress.
    private readonly ConcurrentDictionary<string, ConnectorSyncJobConnectorProgress> _connectors =
        new(StringComparer.OrdinalIgnoreCase);

    private ConnectorSyncJobState _state = ConnectorSyncJobState.Pending;
    private string? _errorMessage;
    private DateTime? _startedAt;
    private DateTime? _completedAt;

    public ConnectorSyncJob(
        Guid jobId,
        TenantContext tenant,
        IReadOnlyList<string> connectorIds,
        SyncRequest request,
        ILogger logger,
        IServiceProvider serviceProvider)
    {
        JobId = jobId;
        _tenant = tenant;
        _connectorIds = connectorIds;
        _request = request;
        _logger = logger;
        _serviceProvider = serviceProvider;

        foreach (var id in connectorIds)
        {
            _connectors[id] = new ConnectorSyncJobConnectorProgress
            {
                ConnectorId = id,
                State = ConnectorSyncJobConnectorState.Pending,
            };
        }
    }

    public Guid JobId { get; }

    public Guid TenantId => _tenant.TenantId;

    public DateTime? CompletedAt => _completedAt;

    public bool IsTerminal =>
        _state is ConnectorSyncJobState.Completed
            or ConnectorSyncJobState.Failed
            or ConnectorSyncJobState.Cancelled;

    public void Cancel()
    {
        _cts.Cancel();
        // Leave terminal states untouched; otherwise mark cancelled so the snapshot reflects intent
        // even before the background loop observes the token.
        if (_state is ConnectorSyncJobState.Pending or ConnectorSyncJobState.Running)
            _state = ConnectorSyncJobState.Cancelled;
    }

    public async Task ExecuteAsync()
    {
        _startedAt = DateTime.UtcNow;
        _state = ConnectorSyncJobState.Running;
        var ct = _cts.Token;

        try
        {
            foreach (var connectorId in _connectorIds)
            {
                ct.ThrowIfCancellationRequested();

                var startedAt = DateTime.UtcNow;
                _connectors[connectorId] = new ConnectorSyncJobConnectorProgress
                {
                    ConnectorId = connectorId,
                    State = ConnectorSyncJobConnectorState.Running,
                    StartedAt = startedAt,
                };

                // Fresh scope per connector with the owning tenant's context applied, so the
                // scoped sync service, DbContext (RLS) and SignalR progress reporter all resolve
                // against the right tenant even though no HTTP request is alive.
                using var scope = _serviceProvider.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantAccessor>().SetTenant(_tenant);
                var syncService = scope.ServiceProvider.GetRequiredService<IConnectorSyncService>();

                // TriggerSyncAsync converts connector failures into a failed SyncResult rather than
                // throwing, so one bad connector doesn't abort the rest of the job.
                var result = await syncService.TriggerSyncAsync(connectorId, _request, ct);

                _connectors[connectorId] = new ConnectorSyncJobConnectorProgress
                {
                    ConnectorId = connectorId,
                    State = result.Success
                        ? ConnectorSyncJobConnectorState.Succeeded
                        : ConnectorSyncJobConnectorState.Failed,
                    StartedAt = startedAt,
                    CompletedAt = DateTime.UtcNow,
                    Message = result.Message,
                    Result = result,
                };
            }

            _state = ConnectorSyncJobState.Completed;
        }
        catch (OperationCanceledException)
        {
            _state = ConnectorSyncJobState.Cancelled;
        }
        catch (Exception ex)
        {
            _state = ConnectorSyncJobState.Failed;
            _errorMessage = ex.Message;
            _logger.LogError(ex, "Connector sync job {JobId} failed", JobId);
        }
        finally
        {
            _completedAt = DateTime.UtcNow;
        }
    }

    public ConnectorSyncJobStatus GetStatus()
    {
        var connectors = _connectorIds
            .Select(id => _connectors[id])
            .ToList();

        return new ConnectorSyncJobStatus
        {
            JobId = JobId,
            State = _state,
            CreatedAt = _createdAt,
            StartedAt = _startedAt,
            CompletedAt = _completedAt,
            ErrorMessage = _errorMessage,
            TotalConnectors = connectors.Count,
            CompletedConnectors = connectors.Count(c =>
                c.State is ConnectorSyncJobConnectorState.Succeeded
                    or ConnectorSyncJobConnectorState.Failed),
            Connectors = connectors,
        };
    }
}

/// <summary>Lifecycle state of a manual connector sync job.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConnectorSyncJobState
{
    /// <summary>Created and queued, not yet started.</summary>
    Pending,
    /// <summary>Actively syncing connectors.</summary>
    Running,
    /// <summary>Every connector has been processed (individual connectors may still have failed).</summary>
    Completed,
    /// <summary>The job terminated due to an unrecoverable error before completing.</summary>
    Failed,
    /// <summary>The job was cancelled before completing.</summary>
    Cancelled,
}

/// <summary>State of a single connector within a sync job.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConnectorSyncJobConnectorState
{
    /// <summary>Queued, not yet started.</summary>
    Pending,
    /// <summary>Currently syncing.</summary>
    Running,
    /// <summary>Sync completed successfully.</summary>
    Succeeded,
    /// <summary>Sync failed.</summary>
    Failed,
}

/// <summary>Progress for a single connector within a sync job.</summary>
public record ConnectorSyncJobConnectorProgress
{
    /// <summary>The connector id (e.g. <c>nightscout</c>).</summary>
    public required string ConnectorId { get; init; }

    /// <summary>The connector's current state in this job.</summary>
    public ConnectorSyncJobConnectorState State { get; init; }

    /// <summary>When this connector's sync started, or null if still pending.</summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>When this connector's sync finished, or null if pending or running.</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>A human-readable message for the outcome, once the connector has completed.</summary>
    public string? Message { get; init; }

    /// <summary>The full sync result, once the connector has completed.</summary>
    public SyncResult? Result { get; init; }
}

/// <summary>A pollable snapshot of a manual connector sync job's progress.</summary>
public record ConnectorSyncJobStatus
{
    /// <summary>The job id, used to poll status and cancel.</summary>
    public required Guid JobId { get; init; }

    /// <summary>The job's current lifecycle state.</summary>
    public required ConnectorSyncJobState State { get; init; }

    /// <summary>When the job was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the background work started, or null if not yet started.</summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>When the job reached a terminal state, or null if still running.</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>An error message when the whole job failed (not a single connector).</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Total connectors the job will sync.</summary>
    public int TotalConnectors { get; init; }

    /// <summary>How many connectors have finished (succeeded or failed).</summary>
    public int CompletedConnectors { get; init; }

    /// <summary>Per-connector progress, in requested order.</summary>
    public IReadOnlyList<ConnectorSyncJobConnectorProgress> Connectors { get; init; } = [];
}
