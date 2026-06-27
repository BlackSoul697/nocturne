using Nocturne.API.Services.Audit;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Core.Contracts.Audit;
using Nocturne.Core.Contracts.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.ConnectorPublishing;

/// <summary>
/// Publishes device status and device event data received from connectors into
/// the Nocturne domain via <see cref="IDeviceStatusDecomposer"/> and <see cref="IDeviceEventRepository"/>.
/// </summary>
/// <seealso cref="IDevicePublisher"/>
internal sealed class DevicePublisher : IDevicePublisher
{
    private readonly IDeviceStatusDecomposer _decomposer;
    private readonly IDeviceEventRepository _deviceEventRepository;
    private readonly IAuditContext _auditContext;
    private readonly IApsSnapshotRepository _apsSnapshotRepository;
    private readonly IPatientDeviceRepository _patientDeviceRepository;
    private readonly ILogger<DevicePublisher> _logger;

    public DevicePublisher(
        IDeviceStatusDecomposer decomposer,
        IDeviceEventRepository deviceEventRepository,
        IAuditContext auditContext,
        IApsSnapshotRepository apsSnapshotRepository,
        IPatientDeviceRepository patientDeviceRepository,
        ILogger<DevicePublisher> logger)
    {
        _decomposer = decomposer ?? throw new ArgumentNullException(nameof(decomposer));
        _deviceEventRepository = deviceEventRepository ?? throw new ArgumentNullException(nameof(deviceEventRepository));
        _auditContext = auditContext;
        _apsSnapshotRepository = apsSnapshotRepository ?? throw new ArgumentNullException(nameof(apsSnapshotRepository));
        _patientDeviceRepository = patientDeviceRepository ?? throw new ArgumentNullException(nameof(patientDeviceRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> PublishPatientDevicesAsync(
        IEnumerable<PatientDevice> devices,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var list = devices.ToList();
            if (list.Count == 0) return true;

            using (SystemAuditScope.Push(_auditContext))
            {
                foreach (var device in list)
                {
                    // Upsert on the connector's deterministic Id so re-syncs update the same row.
                    var existing = await _patientDeviceRepository.GetByIdAsync(device.Id, cancellationToken);
                    if (existing != null)
                        await _patientDeviceRepository.UpdateAsync(device.Id, device, cancellationToken);
                    else
                        await _patientDeviceRepository.CreateAsync(device, cancellationToken);
                }
            }

            _logger.LogDebug("Published {Count} PatientDevice records for {Source}", list.Count, source);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish PatientDevice records for {Source}", source);
            return false;
        }
    }

    public async Task<bool> PublishDeviceStatusAsync(
        IEnumerable<DeviceStatus> deviceStatuses,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var ds in deviceStatuses)
            {
                await _decomposer.DecomposeAsync(ds, cancellationToken);
            }
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish device status for {Source}", source);
            return false;
        }
    }

    public async Task<bool> PublishDeviceEventsAsync(
        IEnumerable<DeviceEvent> records,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var recordList = records.ToList();
            if (recordList.Count == 0) return true;

            using (SystemAuditScope.Push(_auditContext))
                await _deviceEventRepository.BulkCreateAsync(recordList, cancellationToken);
            _logger.LogDebug("Published {Count} DeviceEvent records for {Source}", recordList.Count, source);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish DeviceEvent records for {Source}", source);
            return false;
        }
    }

    /// <summary>
    /// Returns the timestamp of the most recent device-status record for the current tenant,
    /// using the <c>aps_snapshots</c> watermark — the dominant loop/openaps/pump device-status
    /// source after decomposition. Like <see cref="ITreatmentPublisher.GetLatestTreatmentTimestampAsync"/>,
    /// this is not source-filtered and returns the global latest for the tenant.
    /// </summary>
    public async Task<DateTime?> GetLatestDeviceStatusTimestampAsync(
        string source,
        CancellationToken cancellationToken = default)
        => await _apsSnapshotRepository.GetLatestTimestampAsync(null, cancellationToken);
}
