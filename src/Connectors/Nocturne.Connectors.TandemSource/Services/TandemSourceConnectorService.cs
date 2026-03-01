using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.TandemSource.Configurations;
using Nocturne.Connectors.TandemSource.EventParser;
using Nocturne.Connectors.TandemSource.Mappers;
using Nocturne.Connectors.TandemSource.Models;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.TandemSource.Services;

public class TandemSourceConnectorService : BaseConnectorService<TandemSourceConnectorConfiguration>
{
    private readonly TandemSourceAuthTokenProvider _tokenProvider;

    /// <summary>
    /// Default event IDs filter matching the Tandem Source backend's known events.
    /// </summary>
    private static readonly int[] DefaultEventIds =
    [
        229, 5, 28, 4, 26, 99, 279, 3, 16, 59, 21, 55, 20, 280, 64, 65, 66, 61, 33,
        371, 171, 369, 460, 172, 370, 461, 372, 399, 256, 213, 406, 394, 212, 404,
        214, 405, 447, 313, 60, 14, 6, 90, 230, 140, 12, 11, 53, 13, 63, 203, 307, 191
    ];

    public TandemSourceConnectorService(
        HttpClient httpClient,
        ILogger<TandemSourceConnectorService> logger,
        TandemSourceAuthTokenProvider tokenProvider,
        IConnectorPublisher? publisher = null
    ) : base(httpClient, logger, publisher)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
    }

    protected override string ConnectorSource => DataSources.TandemSourceConnector;
    public override string ServiceName => "Tandem Source";

    public override List<SyncDataType> SupportedDataTypes =>
    [
        SyncDataType.Glucose,
        SyncDataType.Boluses,
        SyncDataType.DeviceEvents,
        SyncDataType.StateSpans,
        SyncDataType.Profiles
    ];

    public override async Task<bool> AuthenticateAsync()
    {
        var token = await _tokenProvider.GetValidTokenAsync();
        if (token == null)
        {
            TrackFailedRequest("Failed to get valid token");
            return false;
        }
        TrackSuccessfulRequest();
        return true;
    }

    protected override async Task<SyncResult> PerformSyncInternalAsync(
        SyncRequest request,
        TandemSourceConnectorConfiguration config,
        CancellationToken cancellationToken)
    {
        var result = new SyncResult { StartTime = DateTimeOffset.UtcNow, Success = true };
        var enabledTypes = config.GetEnabledDataTypes(SupportedDataTypes);
        var timezone = GetTimezone(config);
        var region = TandemSourceRegion.ForServer(config.Server);

        try
        {
            var token = await _tokenProvider.GetValidTokenAsync(cancellationToken);
            if (string.IsNullOrEmpty(token))
            {
                result.Success = false;
                result.Errors.Add("Authentication failed");
                result.EndTime = DateTimeOffset.UtcNow;
                return result;
            }

            var pumperId = _tokenProvider.PumperId;
            if (string.IsNullOrEmpty(pumperId))
            {
                result.Success = false;
                result.Errors.Add("PumperId not available after authentication");
                result.EndTime = DateTimeOffset.UtcNow;
                return result;
            }

            // Fetch pump metadata to find the device ID and profile settings
            var metadata = await FetchPumpMetadataAsync(region, pumperId, token, cancellationToken);
            if (metadata == null || metadata.Count == 0)
            {
                result.Success = false;
                result.Errors.Add("No pump metadata returned");
                result.EndTime = DateTimeOffset.UtcNow;
                return result;
            }

            var device = metadata.OrderByDescending(m => m.MaxDateWithEvents).First();
            _logger.LogInformation(
                "Using pump device {DeviceId} (serial: {Serial}, model: {Model})",
                device.TConnectDeviceId, device.SerialNumber, device.ModelNumber);

            var since = await CalculateSinceTimestampAsync(config, request.From);
            var until = request.To ?? DateTime.UtcNow;

            // Fetch and parse events
            var events = await FetchAndParseEventsAsync(
                region, pumperId, device.TConnectDeviceId, since, until, token, cancellationToken);

            if (events.Count > 0)
            {
                _logger.LogInformation("Parsed {Count} events from Tandem Source", events.Count);

                foreach (var evt in events)
                    evt.SetTimezone(timezone);

                var classified = EventClassifier.ClassifyAll(events);
                var classCounts = classified.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value.Count);
                _logger.LogInformation("Classified events: {Counts}", JsonSerializer.Serialize(classCounts));

                // Basal
                if (enabledTypes.Contains(SyncDataType.Glucose) &&
                    classified.TryGetValue(EventClass.Basal, out var basalEvents))
                {
                    var tempBasals = TandemSourceBasalMapper.Map(basalEvents, timezone, until);
                    if (tempBasals.Count > 0)
                    {
                        await PublishTempBasalDataAsync(tempBasals, config, cancellationToken);
                        result.ItemsSynced[SyncDataType.Glucose] =
                            result.ItemsSynced.GetValueOrDefault(SyncDataType.Glucose) + tempBasals.Count;
                        _logger.LogInformation("Synced {Count} basal records", tempBasals.Count);
                    }
                }

                // Boluses
                if (enabledTypes.Contains(SyncDataType.Boluses) &&
                    classified.TryGetValue(EventClass.Bolus, out var bolusEvents))
                {
                    var boluses = TandemSourceBolusMapper.Map(bolusEvents, timezone);
                    if (boluses.Count > 0)
                    {
                        await PublishBolusDataAsync(boluses, config, cancellationToken);
                        result.ItemsSynced[SyncDataType.Boluses] = boluses.Count;
                        _logger.LogInformation("Synced {Count} bolus records", boluses.Count);
                    }
                }

                // CGM readings
                if (enabledTypes.Contains(SyncDataType.Glucose) &&
                    classified.TryGetValue(EventClass.CgmReading, out var cgmEvents))
                {
                    var sgRecords = TandemSourceSensorGlucoseMapper.Map(cgmEvents, timezone);
                    if (sgRecords.Count > 0)
                    {
                        await PublishSensorGlucoseDataAsync(sgRecords, config, cancellationToken);
                        result.ItemsSynced[SyncDataType.Glucose] =
                            result.ItemsSynced.GetValueOrDefault(SyncDataType.Glucose) + sgRecords.Count;
                        _logger.LogInformation("Synced {Count} CGM records", sgRecords.Count);
                    }
                }

                // Device events (alarms, cartridge, CGM alerts, CGM sessions)
                if (enabledTypes.Contains(SyncDataType.DeviceEvents))
                {
                    var deviceEvents = TandemSourceDeviceEventMapper.Map(classified, timezone);
                    if (deviceEvents.Count > 0)
                    {
                        await PublishDeviceEventDataAsync(deviceEvents, config, cancellationToken);
                        result.ItemsSynced[SyncDataType.DeviceEvents] = deviceEvents.Count;
                        _logger.LogInformation("Synced {Count} device event records", deviceEvents.Count);
                    }
                }

                // State spans (suspend/resume, sleep/exercise)
                if (enabledTypes.Contains(SyncDataType.StateSpans))
                {
                    var stateSpans = TandemSourceStateSpanMapper.Map(classified, timezone);
                    if (stateSpans.Count > 0)
                    {
                        await PublishStateSpanDataAsync(stateSpans, config, cancellationToken);
                        result.ItemsSynced[SyncDataType.StateSpans] = stateSpans.Count;
                        _logger.LogInformation("Synced {Count} state span records", stateSpans.Count);
                    }
                }
            }

            // Profiles (from metadata, not from events)
            if (enabledTypes.Contains(SyncDataType.Profiles))
            {
                var pumpSettings = device.LastUpload?.Settings;
                var profile = TandemSourceProfileMapper.Map(pumpSettings, config.Timezone);
                if (profile != null)
                {
                    await PublishProfileDataAsync([profile], config, cancellationToken);
                    result.ItemsSynced[SyncDataType.Profiles] = 1;
                    _logger.LogInformation("Synced profile data");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Tandem Source sync");
            result.Success = false;
            result.Errors.Add($"Sync error: {ex.Message}");
        }

        result.EndTime = DateTimeOffset.UtcNow;
        return result;
    }

    private async Task<List<PumpEventMetadata>?> FetchPumpMetadataAsync(
        TandemSourceRegion region, string pumperId, string token, CancellationToken ct)
    {
        var url = $"{region.SourceBaseUrl}api/reports/reportsfacade/{pumperId}/pumpeventmetadata";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to fetch pump metadata: {StatusCode} {Error}", response.StatusCode, error);
            return null;
        }

        return await DeserializeResponseAsync<List<PumpEventMetadata>>(response);
    }

    private async Task<List<ParsedEvent>> FetchAndParseEventsAsync(
        TandemSourceRegion region, string pumperId, string deviceId,
        DateTime since, DateTime until, string token, CancellationToken ct)
    {
        var minDate = since.ToString("yyyy-MM-dd");
        var maxDate = until.ToString("yyyy-MM-dd");
        var eventIdsFilter = string.Join("%2C", DefaultEventIds);

        var url = $"{region.SourceBaseUrl}api/reports/reportsfacade/pumpevents/{pumperId}/{deviceId}" +
                  $"?minDate={minDate}&maxDate={maxDate}&eventIds={eventIdsFilter}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to fetch pump events: {StatusCode} {Error}", response.StatusCode, error);
            return [];
        }

        var base64Data = await response.Content.ReadAsStringAsync(ct);
        base64Data = base64Data.Trim('"');

        if (string.IsNullOrWhiteSpace(base64Data))
        {
            _logger.LogWarning("Empty pump events response");
            return [];
        }

        try
        {
            var events = EventDefinitionLoader.ParseEvents(base64Data);
            _logger.LogInformation("Decoded {ByteCount} bytes into {EventCount} events",
                Convert.FromBase64String(base64Data).Length, events.Count);
            return events;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to decode base64 pump events data");
            return [];
        }
    }

    private static TimeZoneInfo GetTimezone(TandemSourceConnectorConfiguration config)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(config.Timezone);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }
}
