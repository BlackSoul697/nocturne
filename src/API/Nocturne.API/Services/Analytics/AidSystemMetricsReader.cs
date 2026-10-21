using Nocturne.Core.Clinical.Devices;
using Nocturne.Core.Contracts.Analytics;
using Nocturne.Core.Contracts.Glucose;
using Nocturne.Core.Contracts.Profiles.Resolvers;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.Analytics;

/// <summary>The records <see cref="AidSystemMetrics"/> is assembled from, read for one window.</summary>
/// <param name="Glucose">Canonical readings (<see cref="ICanonicalGlucoseService"/>).</param>
public sealed record AidSystemMetricsRecords(
    IReadOnlyList<PatientDevice> Devices,
    IReadOnlyList<ApsLoopCycle> LoopCycles,
    IReadOnlyList<TempBasal> TempBasals,
    IReadOnlyList<DeviceEvent> DeviceEvents,
    IReadOnlyList<SensorGlucose> Glucose,
    double? TargetLow,
    double? TargetHigh);

/// <summary>
/// Reads and assembles the inputs of <see cref="IAidMetricsService"/>: the pumps' algorithm
/// segments, the APS loop cycles and temp basals, the site changes, the CGM active time and the
/// device names, for a reader that has already fetched them (<see cref="Calculate"/>) or one that
/// has not (<see cref="ReadAsync"/>).
/// </summary>
/// <remarks>
/// A registered device's dates are read as UTC days, and its last day ends at 23:59:59.
/// </remarks>
public sealed class AidSystemMetricsReader(
    IPatientDeviceRepository patientDeviceRepository,
    IApsSnapshotRepository apsSnapshotRepository,
    ITempBasalRepository tempBasalRepository,
    IDeviceEventRepository deviceEventRepository,
    ISensorGlucoseRepository sensorGlucoseRepository,
    ICanonicalGlucoseService canonicalGlucose,
    ITargetRangeScheduleRepository targetRangeScheduleRepository,
    IActiveProfileResolver activeProfileResolver,
    IStatisticsService statisticsService,
    IAidMetricsService aidMetricsService)
{
    /// <summary>
    /// Every collection is read uncapped, for the reason given on
    /// <c>StatisticsController.FetchInsulinRecordsAsync</c>. The target range is the active
    /// profile's now (<see cref="ActiveTargetRange"/>) and is left out if it cannot be read.
    /// </summary>
    public async Task<AidSystemMetrics> ReadAsync(DateTime start, DateTime end, CancellationToken ct)
    {
        var devices = await patientDeviceRepository.GetByDateRangeAsync(start, end, ct: ct);

        var apsTask = apsSnapshotRepository.GetLoopCyclesAsync(start, end, ct);
        var basalTask = tempBasalRepository.GetAsync(start, end, null, null, int.MaxValue, descending: false, ct: ct);
        var eventTask = deviceEventRepository.GetAsync(start, end, null, null, int.MaxValue, descending: false, ct: ct);
        var glucoseTask = sensorGlucoseRepository.GetForAnalyticsAsync(start, end, null, null, int.MaxValue, descending: false, ct: ct);

        await Task.WhenAll(apsTask, basalTask, eventTask, glucoseTask);

        var glucose = await canonicalGlucose.SelectAsync((await glucoseTask).ToList(), ct);

        double? targetLow = null;
        double? targetHigh = null;
        try
        {
            var activeSchedule = await ActiveTargetRange.ScheduleAsync(activeProfileResolver, targetRangeScheduleRepository, ct);
            if (activeSchedule?.Entries.Count > 0)
            {
                targetLow = activeSchedule.Entries.Min(e => e.Low);
                targetHigh = activeSchedule.Entries.Max(e => e.High);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }

        return Calculate(
            new AidSystemMetricsRecords(
                [.. devices],
                [.. await apsTask],
                [.. await basalTask],
                [.. await eventTask],
                glucose,
                targetLow,
                targetHigh),
            start,
            end);
    }

    public AidSystemMetrics Calculate(AidSystemMetricsRecords records, DateTime start, DateTime end)
    {
        var deviceSegments = records.Devices
            .Where(d => d.DeviceCategory == DeviceCategory.InsulinPump && d.AidAlgorithm.HasValue)
            .Select(d => new DeviceSegmentInput
            {
                Algorithm = d.AidAlgorithm!.Value,
                StartDate = d.StartDate.HasValue
                    ? d.StartDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                    : start,
                EndDate = d.EndDate.HasValue
                    ? d.EndDate.Value.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc)
                    : end,
            })
            .ToList();

        var siteChangeCount = records.DeviceEvents.Count(e => e.EventType == DeviceEventType.SiteChange);

        var cgmDevices = records.Devices.Where(d => d.DeviceCategory == DeviceCategory.CGM).ToList();
        var cgmDeviceNames = cgmDevices.Count > 0
            ? string.Join(", ", cgmDevices
                .Select(d => d.CatalogId != null ? DeviceCatalog.GetById(d.CatalogId)?.Name : null)
                .Where(n => n != null)
                .DefaultIfEmpty(cgmDevices.First().Model ?? cgmDevices.First().Manufacturer))
            : null;

        var pumpDevices = records.Devices.Where(d => d.DeviceCategory == DeviceCategory.InsulinPump).ToList();
        var pumpCatalogNames = pumpDevices
            .Select(d => d.CatalogId != null ? DeviceCatalog.GetById(d.CatalogId)?.Name : null)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .ToList();
        var pumpDeviceNames = pumpCatalogNames.Count > 0
            ? string.Join(", ", pumpCatalogNames)
            : pumpDevices
                .Select(d => !string.IsNullOrWhiteSpace(d.Model) ? d.Model : d.Manufacturer)
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

        var cgmActivePercent = statisticsService.CalculateCgmActivePercent(
            records.Glucose,
            start,
            end,
            cgmDevices
                .Select(d => new CgmDeviceWindow(
                    d.Id,
                    d.StartDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    d.EndDate?.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc),
                    d.CatalogId != null ? DeviceCatalog.GetById(d.CatalogId)?.Cgm?.UpdateIntervalMinutes : null))
                .ToList());

        return aidMetricsService.Calculate(
            deviceSegments,
            records.LoopCycles,
            records.TempBasals,
            siteChangeCount,
            cgmDeviceNames,
            pumpDeviceNames,
            cgmActivePercent,
            records.TargetLow,
            records.TargetHigh,
            start,
            end);
    }
}
