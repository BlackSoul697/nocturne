using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.Core.Utilities;
using Nocturne.Connectors.Glooko.Configurations;
using Nocturne.Connectors.Glooko.Mappers;
using Nocturne.Connectors.Glooko.Models;
using Nocturne.Connectors.Glooko.Utilities;
using Nocturne.Core.Constants;
using Nocturne.Core.Contracts.Timezones;
using Nocturne.Core.Contracts.Treatments;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Timezones;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.Glooko.Services;

/// <summary>
///     Connector service for Glooko data source.
///     Based on the original nightscout-connect Glooko implementation.
/// </summary>
public class GlookoConnectorService : BaseConnectorService<GlookoConnectorConfiguration>
{
    private readonly IConnectorPublisher? _connectorPublisher;
    private readonly IMealMatchingService? _mealMatchingService;
    private readonly IRateLimitingStrategy _rateLimitingStrategy;
    private readonly IRetryDelayStrategy _retryDelayStrategy;
    private readonly GlookoAuthTokenProvider _tokenProvider;
    private readonly ITimezoneTimelineService? _timezoneTimelineService;
    private readonly IConnectorSyncCursorStore? _cursorStore;
    private readonly ILogger<GlookoConnectorService> _glookoLogger;

    public GlookoConnectorService(
        HttpClient httpClient,
        IConnectorServerResolver<GlookoConnectorConfiguration> serverResolver,
        ILogger<GlookoConnectorService> logger,
        IRetryDelayStrategy retryDelayStrategy,
        IRateLimitingStrategy rateLimitingStrategy,
        GlookoAuthTokenProvider tokenProvider,
        IConnectorPublisher? publisher = null,
        IMealMatchingService? mealMatchingService = null,
        ITimezoneTimelineService? timezoneTimelineService = null,
        IConnectorSyncCursorStore? cursorStore = null
    )
        : base(httpClient, serverResolver, logger, publisher)
    {
        _connectorPublisher = publisher;
        _mealMatchingService = mealMatchingService;
        _retryDelayStrategy = retryDelayStrategy ?? throw new ArgumentNullException(nameof(retryDelayStrategy));
        _rateLimitingStrategy = rateLimitingStrategy ?? throw new ArgumentNullException(nameof(rateLimitingStrategy));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _timezoneTimelineService = timezoneTimelineService;
        _cursorStore = cursorStore;
        _glookoLogger = logger;
    }

    public override string ServiceName => "Glooko";
    protected override string ConnectorSource => DataSources.GlookoConnector;

    public override List<SyncDataType> SupportedDataTypes =>
    [
        SyncDataType.Glucose,
        SyncDataType.ManualBG,
        SyncDataType.Boluses,
        SyncDataType.BasalInjections,
        SyncDataType.CarbIntake,
        SyncDataType.StateSpans,
        SyncDataType.TempBasals,
        SyncDataType.DeviceEvents,
        SyncDataType.Profiles
    ];

    // ── Per-sync state (populated in PerformSyncInternalAsync) ─────────
    // TODO: These instance fields are not safe for concurrent multi-tenant syncs.
    // They should be refactored to local variables threaded through helper methods.

    private string? _sessionCookie;
    private GlookoUserData? _userData;
    private GlookoConnectorConfiguration? _syncConfig;
    private GlookoTimeMapper? _timeMapper;
    private GlookoSensorGlucoseMapper? _sensorGlucoseMapper;
    private GlookoV4TreatmentMapper? _v4TreatmentMapper;
    private GlookoStateSpanMapper? _stateSpanMapper;
    private GlookoTempBasalMapper? _tempBasalMapper;
    private GlookoSystemEventMapper? _systemEventMapper;
    private GlookoPumpEventMapper? _pumpEventMapper;
    private GlookoDeviceMapper? _deviceMapper;
    private GlookoProfileMapper? _profileMapper;
    private GlookoNoteMapper? _noteMapper;
    private GlookoActivityMapper? _activityMapper;
    private GlookoSettingsProfileMapper? _settingsProfileMapper;

    private void InitializeMappers(GlookoConnectorConfiguration config)
    {
        _syncConfig = config;
        _timeMapper = new GlookoTimeMapper(config, _glookoLogger);
        _sensorGlucoseMapper = new GlookoSensorGlucoseMapper(config, ConnectorSource, _timeMapper, _glookoLogger);
        _v4TreatmentMapper = new GlookoV4TreatmentMapper(ConnectorSource, _timeMapper, _glookoLogger);
        _stateSpanMapper = new GlookoStateSpanMapper(ConnectorSource, _timeMapper, _glookoLogger);
        _tempBasalMapper = new GlookoTempBasalMapper(ConnectorSource, _timeMapper, _glookoLogger);
        _systemEventMapper = new GlookoSystemEventMapper(ConnectorSource, _timeMapper, _glookoLogger);
        _pumpEventMapper = new GlookoPumpEventMapper(ConnectorSource, _timeMapper, _glookoLogger);
        _deviceMapper = new GlookoDeviceMapper(ConnectorSource, _timeMapper, _glookoLogger);
        _profileMapper = new GlookoProfileMapper(ConnectorSource, _glookoLogger);
        _noteMapper = new GlookoNoteMapper(ConnectorSource, _timeMapper, _glookoLogger);
        _activityMapper = new GlookoActivityMapper(ConnectorSource, _timeMapper, _glookoLogger);
        _settingsProfileMapper = new GlookoSettingsProfileMapper(ConnectorSource, _glookoLogger);
    }

    // ── Authentication ──────────────────────────────────────────────────

    public override async Task<bool> AuthenticateAsync()
    {
        // Legacy method; actual auth happens per-tenant in sync flow
        TrackSuccessfulRequest();
        return true;
    }

    private async Task<bool> AuthenticateWithConfigAsync(GlookoConnectorConfiguration config)
    {
        var token = await _tokenProvider.GetValidTokenAsync(config);
        if (token == null)
        {
            TrackFailedRequest("Failed to get valid token");
            return false;
        }

        // The token IS the session cookie for Glooko
        _sessionCookie = token;

        // Retrieve user data from cache metadata via the token provider's public accessor
        var cached = await _tokenProvider.GetCachedSessionAsync();
        if (cached?.Metadata != null && cached.Metadata.TryGetValue("UserData", out var userDataJson))
        {
            _userData = JsonSerializer.Deserialize<GlookoUserData>(userDataJson);
        }

        TrackSuccessfulRequest();
        return true;
    }

    /// <summary>
    ///     Validates that the session is active and the Glooko user code is available.
    ///     Throws <see cref="InvalidOperationException"/> if not authenticated.
    ///     Returns null and logs a warning if the user code is missing.
    /// </summary>
    private string? EnsureAuthenticatedAndGetCode()
    {
        if (string.IsNullOrEmpty(_sessionCookie))
            throw new InvalidOperationException(
                "Not authenticated with Glooko. Call AuthenticateAsync first.");

        var code = _userData?.GlookoCode;
        if (code == null)
            _logger.LogWarning("Missing Glooko user code, cannot fetch data");

        return code;
    }

    private bool IsSessionExpired() => string.IsNullOrEmpty(_sessionCookie);

    // ── HTTP helpers ────────────────────────────────────────────────────

    /// <summary>
    ///     Sends a GET request to a Glooko API endpoint with standard headers.
    ///     Relative paths are resolved against the configured server region.
    /// </summary>
    private async Task<JsonElement?> FetchFromGlookoEndpoint(string url)
    {
        var baseUrl = GlookoConstants.ResolveBaseUrl(_syncConfig!.Server);
        var webOrigin = GlookoConstants.ResolveWebOrigin(_syncConfig!.Server);
        var absoluteUrl = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? url
            : $"{baseUrl}{url}";

        _logger.LogDebug("GLOOKO FETCHER LOADING {Url}", absoluteUrl);

        var request = new HttpRequestMessage(HttpMethod.Get, absoluteUrl);
        GlookoHttpHelper.ApplyStandardHeaders(request, webOrigin, _sessionCookie);

        var response = await _httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var json = await GlookoHttpHelper.ReadResponseAsync(response);
            _logger.LogDebug("[{ConnectorSource}] Response {StatusCode} from {Url}: {Json}",
                ConnectorSource, (int)response.StatusCode, absoluteUrl, json);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            _logger.LogWarning("Rate limited (422) fetching from {Url}", absoluteUrl);
            throw new HttpRequestException("422 UnprocessableEntity - Rate limited");
        }

        _logger.LogWarning("Failed to fetch from {Url}: {StatusCode}", absoluteUrl, response.StatusCode);
        throw new HttpRequestException($"HTTP {(int)response.StatusCode} {response.StatusCode}");
    }

    /// <summary>
    ///     Fetches from a Glooko endpoint with retry logic and exponential backoff.
    /// </summary>
    private async Task<JsonElement?> FetchFromGlookoEndpointWithRetry(string url, int maxRetries = 3)
    {
        HttpRequestException? lastException = null;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var result = await FetchFromGlookoEndpoint(url);
                if (result.HasValue) return result;

                _logger.LogWarning("Attempt {AttemptNumber} failed for {Url}", attempt + 1, url);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("422"))
            {
                lastException = ex;
                _logger.LogWarning("Rate limited (422) on attempt {AttemptNumber} for {Url}", attempt + 1, url);
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                _logger.LogError(ex, "Attempt {AttemptNumber} failed for {Url}", attempt + 1, url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Attempt {AttemptNumber} failed for {Url}", attempt + 1, url);
                lastException = new HttpRequestException($"Request failed: {ex.Message}", ex);
            }

            if (attempt < maxRetries - 1)
            {
                _logger.LogInformation("Applying retry backoff before retry {RetryNumber}", attempt + 2);
                await _retryDelayStrategy.ApplyRetryDelayAsync(attempt);
            }
        }

        _logger.LogError("All {MaxRetries} attempts failed for {Url}", maxRetries, url);
        if (lastException != null) throw lastException;
        throw new HttpRequestException($"All {maxRetries} attempts failed for {url}");
    }

    // ── URL construction ────────────────────────────────────────────────

    private string ConstructV2Url(string endpoint, DateTime startDate, DateTime endDate)
    {
        var patientCode = _userData?.GlookoCode;
        var maxCount = Math.Max(1, (int)Math.Ceiling((endDate - startDate).TotalMinutes / 5));

        return $"{endpoint}?patient={patientCode}"
             + $"&startDate={startDate:yyyy-MM-ddTHH:mm:ss.fffZ}"
             + $"&endDate={endDate:yyyy-MM-ddTHH:mm:ss.fffZ}"
             + $"&lastGuid={GlookoConstants.LegacyLastGuid}"
             + $"&lastUpdatedAt={startDate:yyyy-MM-ddTHH:mm:ss.fffZ}"
             + $"&limit={maxCount}";
    }

    private string ConstructV3GraphUrl(DateTime startDate, DateTime endDate)
    {
        var patientCode = _userData?.GlookoCode;

        var series = GlookoConstants.V3GraphSeries
            .Concat(GlookoConstants.V3PumpModeSeries);

        if (_syncConfig!.V3IncludeCgmBackfill)
            series = series.Concat(GlookoConstants.V3CgmBackfillSeries);

        var seriesParams = string.Join("&", series.Select(s => $"series[]={s}"));

        return $"{GlookoConstants.V3GraphDataPath}?patient={patientCode}"
             + $"&startDate={startDate:yyyy-MM-ddTHH:mm:ss.fffZ}"
             + $"&endDate={endDate:yyyy-MM-ddTHH:mm:ss.fffZ}"
             + $"&{seriesParams}"
             + "&locale=en&insulinTooltips=false&filterBgReadings=false&splitByDay=false";
    }

    /// <summary>
    ///     Builds a v3 graph/data URL requesting ONLY the pump-mode series. Pump operating-mode spans
    ///     (auto/manual/sleep/exercise/...) have no SSV2 equivalent — verified against the decompiled app,
    ///     which only exposes aggregate mode percentages, never per-interval spans — so the SSV2 sync path
    ///     keeps this one slim v3 call for the mode timeline (a fraction of the full graph payload).
    /// </summary>
    private string ConstructV3PumpModeUrl(DateTime startDate, DateTime endDate)
    {
        var patientCode = _userData?.GlookoCode;
        var seriesParams = string.Join("&", GlookoConstants.V3PumpModeSeries.Select(s => $"series[]={s}"));

        return $"{GlookoConstants.V3GraphDataPath}?patient={patientCode}"
             + $"&startDate={startDate:yyyy-MM-ddTHH:mm:ss.fffZ}"
             + $"&endDate={endDate:yyyy-MM-ddTHH:mm:ss.fffZ}"
             + $"&{seriesParams}"
             + "&locale=en&insulinTooltips=false&filterBgReadings=false&splitByDay=false";
    }

    /// <summary>
    ///     Fetches ONLY the v3 pump-mode series (see <see cref="ConstructV3PumpModeUrl"/>). Returns null on
    ///     any failure — including a 403 from a stale patient code — so a mode-fetch problem degrades to
    ///     "no mode spans this pass" rather than failing the SSV2 sync.
    /// </summary>
    private async Task<GlookoV3GraphResponse?> FetchV3PumpModeGraphAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var patientCode = EnsureAuthenticatedAndGetCode();
            if (patientCode == null) return null;

            var url = ConstructV3PumpModeUrl(startDate, endDate);
            var result = await FetchFromGlookoEndpointWithRetry(url);
            if (!result.HasValue) return null;

            return JsonSerializer.Deserialize<GlookoV3GraphResponse>(result.Value.GetRawText());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[{ConnectorSource}] Failed to fetch v3 pump-mode series; mode state spans skipped this pass",
                ConnectorSource);
            return null;
        }
    }

    // ── Sync orchestration ──────────────────────────────────────────────

    protected override async Task<SyncResult> PerformSyncInternalAsync(
        SyncRequest request,
        GlookoConnectorConfiguration config,
        CancellationToken cancellationToken,
        ISyncProgressReporter? progressReporter = null
    )
    {
        var result = new SyncResult
        {
            Success = true,
            Message = "Sync completed successfully",
            StartTime = DateTime.UtcNow
        };

        try
        {
            InitializeMappers(config);
            await ReportMessageAsync(progressReporter, SyncMessageType.Authenticating, null, cancellationToken);

            if (IsSessionExpired())
                if (!await AuthenticateWithConfigAsync(config))
                {
                    result.Success = false;
                    result.Message = "Authentication failed";
                    result.Errors.Add("Authentication failed");
                    return result;
                }

            if (!request.DataTypes.Any())
                request.DataTypes = SupportedDataTypes;
            var enabledTypes = config.GetEnabledDataTypes(SupportedDataTypes);
            var activeTypes = request.DataTypes.Where(t => enabledTypes.Contains(t)).ToHashSet();

            // Resolve the tenant's timezone timeline before mapping any records. The account's home
            // zone (from the V3 profile) seeds the timeline's origin on first sync; thereafter the
            // user's travel/relocation entries drive per-record conversion. Falls back to the legacy
            // static offset when the timeline is empty (e.g. V2-only accounts, or profile tz unknown).
            await ConfigureTimezoneTimelineAsync(config, cancellationToken);

            // The request window is real-UTC; Glooko queries expect fake-UTC (local wall-clock). Pad by
            // a day each side so a non-zero offset between the two never clips edge data (dedup absorbs
            // the overlap).
            var from = request.From.HasValue
                ? _timeMapper.ToGlookoTime(request.From.Value).AddDays(-1)
                : _timeMapper.ToGlookoTime(DateTime.UtcNow.AddMonths(-6)).AddDays(-1);
            var to = _timeMapper.ToGlookoTime(DateTime.UtcNow).AddDays(1);

            if (config.UseSsv2Sync)
            {
                // Explicit-range mode (reset/backfill, signalled by a sync window) bypasses stored cursors
                // and re-scans from the beginning; normal background syncs resume incrementally from them.
                await FetchAndMapViaSsv2Async(from, !request.To.HasValue, activeTypes, result, config, cancellationToken);
            }
            else
            {
                var chunks = DateChunker.Chunk(from, to, TimeSpan.FromDays(14)).ToList();

                _logger.LogInformation(
                    "[{ConnectorSource}] Syncing {From:yyyy-MM-dd} to {To:yyyy-MM-dd} in {ChunkCount} chunk(s)",
                    ConnectorSource, from, to, chunks.Count);

                for (var i = 0; i < chunks.Count; i++)
                {
                    var (chunkFrom, chunkTo) = chunks[i];

                    await ReportMessageAsync(progressReporter, SyncMessageType.FetchingData,
                        new()
                        {
                            ["from"] = chunkFrom.ToString("MMM dd"),
                            ["to"] = chunkTo.ToString("MMM dd"),
                            ["chunk"] = $"{i + 1}/{chunks.Count}",
                        },
                        cancellationToken);

                    var chunkSuccess = _syncConfig!.UseV3Api
                        ? await FetchAndMapViaV3Async(chunkFrom, chunkTo, activeTypes, result, config, cancellationToken)
                        : await FetchAndMapViaV2Async(chunkFrom, chunkTo, activeTypes, result, config, cancellationToken);

                    if (!chunkSuccess)
                    {
                        _logger.LogWarning(
                            "[{ConnectorSource}] Chunk {Chunk}/{Total} ({From:yyyy-MM-dd} to {To:yyyy-MM-dd}) failed, stopping sync",
                            ConnectorSource, i + 1, chunks.Count, chunkFrom, chunkTo);
                        result.Success = false;
                        result.Message = "Sync failed during data fetch";
                        result.Errors.Add($"Chunk {i + 1}/{chunks.Count} failed ({chunkFrom:yyyy-MM-dd} to {chunkTo:yyyy-MM-dd})");
                        break;
                    }

                    _logger.LogInformation(
                        "[{ConnectorSource}] Completed chunk {Chunk}/{Total} ({From:yyyy-MM-dd} to {To:yyyy-MM-dd})",
                        ConnectorSource, i + 1, chunks.Count, chunkFrom, chunkTo);
                }
            }

            // Profiles. The SSV2 path sources these natively from pumps/settings inside
            // FetchAndMapViaSsv2Async; the v2/v3 windowed paths use this v3 devices_and_settings call (no
            // v2 equivalent). Guarded so SSV2 syncs don't also make the v3 call.
            await ReportMessageAsync(progressReporter, SyncMessageType.ProcessingDataType,
                new() { ["dataType"] = SyncDataType.Profiles.ToString() }, cancellationToken);

            if (!config.UseSsv2Sync && activeTypes.Contains(SyncDataType.Profiles))
            {
                try
                {
                    var deviceSettings = await FetchV3DeviceSettingsAsync();
                    if (deviceSettings != null)
                    {
                        var profiles = _profileMapper.TransformDeviceSettingsToProfiles(deviceSettings);
                        if (profiles.Any() && await PublishProfileDataAsync(profiles, config, cancellationToken))
                        {
                            result.ItemsSynced[SyncDataType.Profiles] = profiles.Count;
                            _logger.LogInformation("[{ConnectorSource}] Published {Count} profiles from device settings",
                                ConnectorSource, profiles.Count);
                        }

                        var profileStateSpans = _profileMapper.TransformDeviceSettingsToStateSpans(deviceSettings);
                        if (profileStateSpans.Count > 0)
                        {
                            await PublishStateSpanDataAsync(profileStateSpans, config, cancellationToken);
                            _logger.LogInformation("[{ConnectorSource}] Published {Count} profile state spans from device settings",
                                ConnectorSource, profileStateSpans.Count);
                        }
                    }
                }
                catch (Exception profileEx)
                {
                    _logger.LogWarning(profileEx, "[{ConnectorSource}] Failed to fetch/publish profile data", ConnectorSource);
                }
            }

            await ReportMessageAsync(progressReporter,
                result.Success ? SyncMessageType.SyncComplete : SyncMessageType.SyncFailed,
                null, cancellationToken);

            result.EndTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Glooko batch sync");
            result.Success = false;
            result.Message = "Sync failed with exception";
            result.Errors.Add(ex.Message);
            await ReportMessageAsync(progressReporter, SyncMessageType.SyncFailed, null, cancellationToken);
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    // ── V2 fetch + map ──────────────────────────────────────────────────

    /// <summary>
    ///     Fetches from all V2 endpoints, maps each record type, and publishes inline.
    /// </summary>
    private async Task<bool> FetchAndMapViaV2Async(
        DateTime fromDate,
        DateTime toDate,
        HashSet<SyncDataType> activeTypes,
        SyncResult result,
        GlookoConnectorConfiguration config,
        CancellationToken cancellationToken)
    {
        var batchData = await FetchBatchDataAsync(fromDate, toDate);
        if (batchData == null) return false;

        await MapAndPublishV2BatchAsync(batchData, activeTypes, result, config, cancellationToken);
        return true;
    }

    /// <summary>
    ///     Maps and publishes a populated <see cref="GlookoBatchData"/> (glucose, manual BG, treatments,
    ///     foods, state spans, temp basals). Shared by the date-windowed V2 path and the SSV2 cursor path,
    ///     which differ only in how the batch is fetched.
    /// </summary>
    private async Task MapAndPublishV2BatchAsync(
        GlookoBatchData batchData,
        HashSet<SyncDataType> activeTypes,
        SyncResult result,
        GlookoConnectorConfiguration config,
        CancellationToken cancellationToken)
    {
        // 1. Glucose
        var sensorGlucose = _sensorGlucoseMapper.TransformBatchDataToSensorGlucose(batchData).ToList();
        await PublishRecordTypeAsync(result, SyncDataType.Glucose, activeTypes,
            sensorGlucose, PublishSensorGlucoseDataAsync, config, cancellationToken);
        UpdateLastEntryTime(result, SyncDataType.Glucose, sensorGlucose);

        var bgChecks = _sensorGlucoseMapper.TransformBatchDataToBGChecks(batchData).ToList();
        await PublishRecordTypeAsync(result, SyncDataType.ManualBG, activeTypes,
            bgChecks, PublishBGCheckDataAsync, config, cancellationToken);

        // 2. Treatments (FK order: batches → boluses → carbs+foods)
        var (boluses, carbs, batches) = _v4TreatmentMapper.MapBatchData(batchData);

        if (batches.Count > 0)
            await PublishDecompositionBatchesAsync(batches, config, cancellationToken);

        await PublishRecordTypeAsync(result, SyncDataType.Boluses, activeTypes,
            boluses, PublishBolusDataAsync, config, cancellationToken);

        await PublishRecordTypeAsync(result, SyncDataType.CarbIntake, activeTypes,
            carbs, PublishCarbIntakeDataAsync, config, cancellationToken);

        // 3. Foods + attribution (coupled with carbs)
        var foodEntryImports = batchData.Foods is { Length: > 0 }
            ? _v4TreatmentMapper.MapFoodsToConnectorEntries(batchData) : [];
        Func<string, string?> foodResolver = externalEntryId => $"glooko_food_{externalEntryId}";
        await PublishFoodEntriesAndAttributeAsync(
            foodEntryImports, carbs, foodResolver, config, cancellationToken);

        // 4. State spans
        if (activeTypes.Contains(SyncDataType.StateSpans))
        {
            var stateSpans = _stateSpanMapper.TransformV2ToStateSpans(batchData);
            if (stateSpans.Count > 0)
                await PublishStateSpanDataAsync(stateSpans, config, cancellationToken);
        }

        // 5. Temp basals
        if (activeTypes.Contains(SyncDataType.TempBasals))
        {
            var tempBasals = _tempBasalMapper.TransformV2ToTempBasals(batchData);
            if (tempBasals.Count > 0 && await PublishTempBasalDataAsync(tempBasals, config, cancellationToken))
                result.ItemsSynced[SyncDataType.TempBasals] = tempBasals.Count;
        }
    }

    // ── V3 fetch + map ──────────────────────────────────────────────────

    /// <summary>
    ///     Fetches from V3 graph/data and histories endpoints, maps each record type, and publishes inline.
    /// </summary>
    private async Task<bool> FetchAndMapViaV3Async(
        DateTime fromDate,
        DateTime toDate,
        HashSet<SyncDataType> activeTypes,
        SyncResult result,
        GlookoConnectorConfiguration config,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{ConnectorSource}] Fetching data from v3 API...", ConnectorSource);

        var v3Data = await FetchV3GraphDataAsync(fromDate, toDate);
        if (v3Data == null) return false;

        GlookoV3HistoriesResponse? v3Histories = null;
        try { v3Histories = await FetchV3HistoriesAsync(fromDate, toDate); }
        catch (Exception histEx)
        {
            _logger.LogWarning(histEx, "[{ConnectorSource}] V3 histories fetch failed, meal data will be unavailable", ConnectorSource);
        }

        // 1. Glucose
        if (_syncConfig!.V3IncludeCgmBackfill)
        {
            var sensorGlucose = _sensorGlucoseMapper.TransformV3ToSensorGlucose(v3Data, _meterUnits).ToList();
            await PublishRecordTypeAsync(result, SyncDataType.Glucose, activeTypes,
                sensorGlucose, PublishSensorGlucoseDataAsync, config, cancellationToken);
            UpdateLastEntryTime(result, SyncDataType.Glucose, sensorGlucose);
        }

        var bgChecks = _sensorGlucoseMapper.TransformV3ToBGChecks(v3Data, _meterUnits).ToList();
        await PublishRecordTypeAsync(result, SyncDataType.ManualBG, activeTypes,
            bgChecks, PublishBGCheckDataAsync, config, cancellationToken);

        // 2. Treatments (FK order: batches → boluses → carbs+foods)
        var (v3Boluses, v3BolusCarbIntakes, v3Batches) = _v4TreatmentMapper.MapV3Boluses(v3Data);

        // Carbs: bolus wizard + history meals (preferred) or carbAll (fallback)
        var allCarbs = new List<CarbIntake>(v3BolusCarbIntakes);
        var historyMealCarbs = v3Histories?.Histories != null
            ? _v4TreatmentMapper.MapV3HistoryMealsToCarbIntakes(v3Histories) : [];

        if (historyMealCarbs.Count > 0)
            allCarbs.AddRange(historyMealCarbs);
        else
            allCarbs.AddRange(_v4TreatmentMapper.MapV3CarbAll(v3Data));

        if (v3Batches.Count > 0)
            await PublishDecompositionBatchesAsync(v3Batches, config, cancellationToken);

        await PublishRecordTypeAsync(result, SyncDataType.Boluses, activeTypes,
            v3Boluses, PublishBolusDataAsync, config, cancellationToken);

        await PublishRecordTypeAsync(result, SyncDataType.CarbIntake, activeTypes,
            allCarbs, PublishCarbIntakeDataAsync, config, cancellationToken);

        // 2b. Manual insulin (pen injections: gkInsulinBasal → BasalInjection, gkInsulinBolus → Bolus)
        var (manualBasalInjections, manualBoluses) = _v4TreatmentMapper.MapV3ManualInsulin(v3Data);

        await PublishRecordTypeAsync(result, SyncDataType.Boluses, activeTypes,
            manualBoluses, PublishBolusDataAsync, config, cancellationToken);

        await PublishRecordTypeAsync(result, SyncDataType.BasalInjections, activeTypes,
            manualBasalInjections, PublishBasalInjectionDataAsync, config, cancellationToken);

        // 3. Foods + attribution (coupled with carbs)
        GlookoFood[]? v2Foods = null;
        if (historyMealCarbs.Count > 0)
        {
            try { v2Foods = await FetchV2FoodsAsync(fromDate, toDate); }
            catch (Exception v2Ex)
            {
                _logger.LogWarning(v2Ex, "[{ConnectorSource}] V2 foods fetch failed, food entries will lack externalId/brand metadata", ConnectorSource);
            }
        }

        var foodEntryImports = historyMealCarbs.Count > 0 && v3Histories?.Histories != null
            ? _v4TreatmentMapper.MapV3HistoryMealsToConnectorEntries(v3Histories, v2Foods) : [];

        // Build food resolver
        Func<string, string?>? foodResolver = null;
        if (historyMealCarbs.Count > 0 && v3Histories?.Histories != null)
        {
            var foodGuidToMealGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var meal in GlookoV4TreatmentMapper.ExtractMeals(v3Histories))
            {
                if (meal.SoftDeleted == true || string.IsNullOrEmpty(meal.Guid) || meal.Foods == null) continue;
                foreach (var food in meal.Foods)
                {
                    if (food.SoftDeleted != true && !string.IsNullOrEmpty(food.Guid))
                        foodGuidToMealGuid.TryAdd(food.Guid, meal.Guid!);
                }
            }

            foodResolver = externalEntryId =>
                foodGuidToMealGuid.TryGetValue(externalEntryId, out var mealGuid)
                    ? $"glooko_v3meal_{mealGuid}" : null;
        }

        await PublishFoodEntriesAndAttributeAsync(
            foodEntryImports, allCarbs, foodResolver, config, cancellationToken);

        // 4. State spans
        if (activeTypes.Contains(SyncDataType.StateSpans))
        {
            var stateSpans = _stateSpanMapper.TransformV3ToStateSpans(v3Data);
            stateSpans.AddRange(_stateSpanMapper.TransformV3PumpModeToStateSpans(v3Data));
            if (stateSpans.Count > 0)
                await PublishStateSpanDataAsync(stateSpans, config, cancellationToken);
        }

        // 4b. Temp basals
        if (activeTypes.Contains(SyncDataType.TempBasals))
        {
            var tempBasals = _tempBasalMapper.TransformV3ToTempBasals(v3Data);
            if (tempBasals.Count > 0 && await PublishTempBasalDataAsync(tempBasals, config, cancellationToken))
                result.ItemsSynced[SyncDataType.TempBasals] = tempBasals.Count;
        }

        // 5. Device events + system events (summed into single ItemsSynced entry)
        if (activeTypes.Contains(SyncDataType.DeviceEvents))
        {
            var deviceEventCount = 0;

            var deviceEvents = _v4TreatmentMapper.MapV3DeviceEvents(v3Data);
            if (deviceEvents.Count > 0 && await PublishDeviceEventDataAsync(deviceEvents, config, cancellationToken))
                deviceEventCount += deviceEvents.Count;

            var systemEvents = _systemEventMapper.TransformV3ToSystemEvents(v3Data);
            if (systemEvents.Count > 0 && await PublishSystemEventDataAsync(systemEvents, config, cancellationToken))
                deviceEventCount += systemEvents.Count;

            if (deviceEventCount > 0)
                result.ItemsSynced[SyncDataType.DeviceEvents] = deviceEventCount;
        }

        return true;
    }

    // ── Food attribution helper ────────────────────────────────────────

    /// <summary>
    ///     Publishes food catalog entries and attributes them to carb intakes via the meal matching service.
    /// </summary>
    private async Task PublishFoodEntriesAndAttributeAsync(
        List<ConnectorFoodEntryImport> foodEntryImports,
        List<CarbIntake> carbIntakes,
        Func<string, string?>? foodEntryToCarbLegacyId,
        GlookoConnectorConfiguration config,
        CancellationToken cancellationToken)
    {
        if (foodEntryImports.Count == 0 || _connectorPublisher is not { IsAvailable: true })
            return;

        var importedEntries = await _connectorPublisher.Metadata.PublishConnectorFoodEntriesAsync(
            foodEntryImports, ConnectorSource, cancellationToken);

        if (importedEntries is not { Count: > 0 })
            return;

        _logger.LogInformation("[{ConnectorSource}] Published {Count} food entries to connector food catalog",
            ConnectorSource, importedEntries.Count);

        if (_mealMatchingService == null || carbIntakes.Count == 0 || foodEntryToCarbLegacyId == null)
            return;

        var pendingEntries = importedEntries
            .Where(e => e.Status == ConnectorFoodEntryStatus.Pending)
            .ToList();

        if (pendingEntries.Count == 0) return;

        var carbsByLegacyId = carbIntakes
            .Where(ci => ci.LegacyId != null)
            .ToDictionary(ci => ci.LegacyId!, StringComparer.OrdinalIgnoreCase);

        var attributedCount = 0;

        foreach (var entry in pendingEntries)
        {
            var legacyKey = foodEntryToCarbLegacyId(entry.ExternalEntryId);
            if (legacyKey == null || !carbsByLegacyId.TryGetValue(legacyKey, out var carbIntake))
                continue;

            try
            {
                await _mealMatchingService.AcceptMatchAsync(
                    entry.Id, carbIntake.Id, entry.Carbs, timeOffsetMinutes: 0, cancellationToken);
                attributedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[{ConnectorSource}] Failed to attribute food entry {FoodEntryId} to CarbIntake {CarbIntakeId}",
                    ConnectorSource, entry.Id, carbIntake.Id);
            }
        }

        _logger.LogInformation("[{ConnectorSource}] Attributed {Count}/{Total} food entries to carb intakes",
            ConnectorSource, attributedCount, pendingEntries.Count);
    }

    /// <summary>
    ///     Updates <see cref="SyncResult.LastEntryTimes"/> with the most recent glucose timestamp,
    ///     keeping the max across multiple chunks.
    /// </summary>
    private static void UpdateLastEntryTime(SyncResult result, SyncDataType dataType, List<SensorGlucose> records)
    {
        if (records.Count == 0) return;
        var maxTime = DateTimeOffset.FromUnixTimeMilliseconds(records.Max(s => s.Mills)).UtcDateTime;
        if (!result.LastEntryTimes.TryGetValue(dataType, out var existing) || maxTime > existing)
            result.LastEntryTimes[dataType] = maxTime;
    }

    // ── V2 batch data fetching ──────────────────────────────────────────

    /// <summary>
    ///     Fetches comprehensive batch data from all v2 Glooko endpoints.
    /// </summary>
    public async Task<GlookoBatchData?> FetchBatchDataAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            var patientCode = EnsureAuthenticatedAndGetCode();
            if (patientCode == null) return null;

            _logger.LogInformation("Fetching comprehensive Glooko data from {From:yyyy-MM-dd} to {To:yyyy-MM-dd}", fromDate, toDate);

            var batchData = new GlookoBatchData();

            var endpointDefinitions = new (string Endpoint, Action<JsonElement> Handler)[]
            {
                (GlookoConstants.FoodsPath, json =>
                {
                    if (json.TryGetProperty("foods", out var el))
                        batchData.Foods = JsonSerializer.Deserialize<GlookoFood[]>(el.GetRawText()) ?? [];
                }),
                (GlookoConstants.ScheduledBasalsPath, json =>
                {
                    if (json.TryGetProperty("scheduledBasals", out var el))
                        batchData.ScheduledBasals = JsonSerializer.Deserialize<GlookoBasal[]>(el.GetRawText()) ?? [];
                }),
                (GlookoConstants.NormalBolusesPath, json =>
                {
                    if (json.TryGetProperty("normalBoluses", out var el))
                        batchData.NormalBoluses = JsonSerializer.Deserialize<GlookoBolus[]>(el.GetRawText()) ?? [];
                }),
                (GlookoConstants.CgmReadingsPath, json =>
                {
                    if (json.TryGetProperty("readings", out var el))
                        batchData.Readings = JsonSerializer.Deserialize<GlookoCgmReading[]>(el.GetRawText()) ?? [];
                }),
                (GlookoConstants.MeterReadingsPath, json =>
                {
                    if (json.TryGetProperty("readings", out var el))
                        batchData.MeterReadings = JsonSerializer.Deserialize<GlookoMeterReading[]>(el.GetRawText()) ?? [];
                }),
                (GlookoConstants.SuspendBasalsPath, json =>
                {
                    if (json.TryGetProperty("suspendBasals", out var el))
                        batchData.SuspendBasals = JsonSerializer.Deserialize<GlookoSuspendBasal[]>(el.GetRawText()) ?? [];
                }),
                (GlookoConstants.TemporaryBasalsPath, json =>
                {
                    if (json.TryGetProperty("temporaryBasals", out var el))
                        batchData.TempBasals = JsonSerializer.Deserialize<GlookoTempBasal[]>(el.GetRawText()) ?? [];
                }),
            };

            for (var i = 0; i < endpointDefinitions.Length; i++)
            {
                var (endpoint, handler) = endpointDefinitions[i];
                var url = ConstructV2Url(endpoint, fromDate, toDate);

                await _rateLimitingStrategy.ApplyDelayAsync(i);

                try
                {
                    var fetchResult = await FetchFromGlookoEndpointWithRetry(url);
                    if (fetchResult.HasValue)
                    {
                        try { handler(fetchResult.Value); }
                        catch (Exception ex) { _logger.LogWarning(ex, "Error parsing data from {Endpoint}", endpoint); }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch from {Endpoint}. Continuing with other endpoints.", endpoint);
                }
            }

            _logger.LogInformation(
                "[{ConnectorSource}] Fetched Glooko batch data summary: "
                + "Readings={ReadingsCount}, MeterReadings={MeterReadingsCount}, Foods={FoodsCount}, "
                + "NormalBoluses={BolusCount}, TempBasals={TempBasalCount}, "
                + "ScheduledBasals={ScheduledBasalCount}, Suspends={SuspendCount}",
                ConnectorSource,
                batchData.Readings?.Length ?? 0,
                batchData.MeterReadings?.Length ?? 0,
                batchData.Foods?.Length ?? 0,
                batchData.NormalBoluses?.Length ?? 0,
                batchData.TempBasals?.Length ?? 0,
                batchData.ScheduledBasals?.Length ?? 0,
                batchData.SuspendBasals?.Length ?? 0);

            return batchData;
        }
        catch (InvalidOperationException) { throw; }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Glooko batch data");
            return null;
        }
    }

    // ── V3 data fetching ────────────────────────────────────────────────

    /// <summary>
    ///     Fetches only the V2 foods endpoint. Used by the V3 sync path to get
    ///     rich food metadata (externalId, brand) that V3 histories doesn't provide.
    /// </summary>
    public async Task<GlookoFood[]?> FetchV2FoodsAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            var patientCode = EnsureAuthenticatedAndGetCode();
            if (patientCode == null) return null;

            var url = ConstructV2Url(GlookoConstants.FoodsPath, fromDate, toDate);
            var result = await FetchFromGlookoEndpointWithRetry(url);
            if (!result.HasValue) return null;

            if (result.Value.TryGetProperty("foods", out var el))
            {
                var foods = JsonSerializer.Deserialize<GlookoFood[]>(el.GetRawText()) ?? [];
                _logger.LogInformation("[{ConnectorSource}] Fetched {Count} V2 food records for metadata enrichment",
                    ConnectorSource, foods.Length);
                return foods;
            }

            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{ConnectorSource}] Failed to fetch V2 foods for metadata enrichment", ConnectorSource);
            return null;
        }
    }

    // ── SSV2 granular sync ──────────────────────────────────────────────

    /// <summary>
    ///     Fetches every record of an SSV2 resource via cursor pagination, returning the raw records for the
    ///     caller to map. In incremental mode the scan resumes from (and persists) the stored per-resource
    ///     cursor so only server-side updates since the last sync are pulled; in explicit-range mode (the
    ///     reset/backfill path, signalled by a non-null sync window) the stored cursor is bypassed and the
    ///     scan runs from the beginning, leaving the incremental cursor untouched.
    /// </summary>
    /// <param name="resource">SSV2 resource path (e.g. <c>/api/v2/cgm/egvs</c>).</param>
    /// <param name="selectRecords">Projects a deserialized page to its record array.</param>
    /// <param name="incremental">When true, resume from and persist the stored cursor.</param>
    /// <param name="startDate">Optional clinical-time floor (fake-UTC). Required by egvs; omitted otherwise.</param>
    private async Task<List<TRecord>> FetchSsv2Async<TPage, TRecord>(
        string resource,
        Func<TPage, TRecord[]?> selectRecords,
        bool incremental,
        DateTime? startDate)
        where TPage : GlookoSsv2Page
    {
        EnsureAuthenticatedAndGetCode();

        var stored = incremental && _cursorStore != null
            ? await _cursorStore.GetAsync(ServiceName, resource)
            : null;

        var initialUpdatedAt = stored?.LastUpdatedAt ?? GlookoConstants.Ssv2InitialLastUpdatedAt;
        var initialGuid = stored?.LastGuid ?? GlookoConstants.Ssv2InitialLastGuid;
        var lastUpdatedAt = initialUpdatedAt;
        var lastGuid = initialGuid;

        var all = new List<TRecord>();

        for (var page = 0; page < GlookoConstants.Ssv2MaxPages; page++)
        {
            var url = ConstructSsv2Url(resource, startDate, lastUpdatedAt, lastGuid);
            var result = await FetchFromGlookoEndpointWithRetry(url);
            if (!result.HasValue) break;

            var pageData = JsonSerializer.Deserialize<TPage>(result.Value.GetRawText());
            var batch = pageData == null ? null : selectRecords(pageData);

            if (batch is { Length: > 0 })
                all.AddRange(batch);

            // Stop on a null/empty page.
            if (pageData == null || batch is not { Length: > 0 })
                break;

            // Advance to this page's resume watermark *before* the last-page check, so the final page's
            // cursor is captured too — otherwise the next incremental sync re-fetches that page.
            var prevUpdatedAt = lastUpdatedAt;
            var prevGuid = lastGuid;
            lastUpdatedAt = pageData.LastUpdatedAt ?? lastUpdatedAt;
            lastGuid = pageData.LastGuid ?? lastGuid;

            if (pageData.LastPage)
                break;

            // Loop guard: a non-last page that fails to move the cursor would otherwise spin forever.
            if (lastUpdatedAt == prevUpdatedAt && lastGuid == prevGuid)
            {
                _logger.LogWarning("[{ConnectorSource}] SSV2 {Resource} cursor did not advance; stopping pagination",
                    ConnectorSource, resource);
                break;
            }
        }

        // Persist only for incremental scans, and only when the cursor actually advanced from where this
        // run started (so a no-op pass never rewrites the stored watermark or the epoch default).
        if (incremental && _cursorStore != null
            && (lastUpdatedAt != initialUpdatedAt || lastGuid != initialGuid))
            await _cursorStore.SetAsync(ServiceName, resource, new ConnectorSyncCursor(lastUpdatedAt, lastGuid));

        _logger.LogInformation("[{ConnectorSource}] SSV2 {Resource} fetched {Count} records (incremental={Incremental})",
            ConnectorSource, resource, all.Count, incremental);
        return all;
    }

    /// <summary>
    ///     Fetches the granular <c>cgm/egvs</c> stream and maps it to SensorGlucose.
    /// </summary>
    public async Task<List<SensorGlucose>> FetchSsv2EgvsAsync(bool incremental, DateTime? startDate)
    {
        var egvs = await FetchSsv2Async<GlookoEgvPage, GlookoEgv>(
            GlookoConstants.Ssv2EgvsPath, p => p.Egvs, incremental, startDate);
        return _sensorGlucoseMapper!.TransformEgvsToSensorGlucose(egvs).ToList();
    }

    /// <summary>
    ///     SSV2 sync pass: glucose from the granular egvs feed, and boluses / carbs / manual BG / state
    ///     spans / temp basals via the same v2 batch mappers but fetched incrementally by cursor. In
    ///     incremental mode each resource omits <c>startDate</c> and relies solely on its stored cursor;
    ///     a backfill passes the clinical floor and bypasses the cursor.
    ///     Each resource is fetched in isolation: if one feed fails (network, server error, malformed
    ///     page) it is logged and skipped so the rest of the pass still imports, mirroring the
    ///     per-endpoint resilience of the windowed batch path.
    /// </summary>
    private async Task FetchAndMapViaSsv2Async(
        DateTime from,
        bool incremental,
        HashSet<SyncDataType> activeTypes,
        SyncResult result,
        GlookoConnectorConfiguration config,
        CancellationToken cancellationToken)
    {
        // Incremental syncs resume purely from each resource's stored cursor; an explicit-range backfill
        // passes the clinical floor. (egvs ignores startDate server-side — its cursor is authoritative —
        // but it is kept consistent with the other resources rather than special-cased.)
        DateTime? batchStart = incremental ? null : from;

        if (activeTypes.Contains(SyncDataType.Glucose))
        {
            var egvGlucose = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2EgvsPath,
                () => FetchSsv2EgvsAsync(incremental, batchStart),
                new List<SensorGlucose>());
            if (egvGlucose.Count > 0)
            {
                await PublishRecordTypeAsync(result, SyncDataType.Glucose, activeTypes,
                    egvGlucose, PublishSensorGlucoseDataAsync, config, cancellationToken);
                UpdateLastEntryTime(result, SyncDataType.Glucose, egvGlucose);
            }
        }

        var batchData = new GlookoBatchData
        {
            NormalBoluses = await FetchSsv2BatchResourceAsync<GlookoNormalBolusPage, GlookoBolus>(
                GlookoConstants.NormalBolusesPath, p => p.NormalBoluses, incremental, batchStart),
            ScheduledBasals = await FetchSsv2BatchResourceAsync<GlookoScheduledBasalPage, GlookoBasal>(
                GlookoConstants.ScheduledBasalsPath, p => p.ScheduledBasals, incremental, batchStart),
            TempBasals = await FetchSsv2BatchResourceAsync<GlookoTemporaryBasalPage, GlookoTempBasal>(
                GlookoConstants.TemporaryBasalsPath, p => p.TemporaryBasals, incremental, batchStart),
            SuspendBasals = await FetchSsv2BatchResourceAsync<GlookoSuspendBasalPage, GlookoSuspendBasal>(
                GlookoConstants.SuspendBasalsPath, p => p.SuspendBasals, incremental, batchStart),
            MeterReadings = await FetchSsv2BatchResourceAsync<GlookoMeterReadingPage, GlookoMeterReading>(
                GlookoConstants.MeterReadingsPath, p => p.Readings, incremental, batchStart),
            Foods = await FetchSsv2BatchResourceAsync<GlookoFoodPage, GlookoFood>(
                GlookoConstants.FoodsPath, p => p.Foods, incremental, batchStart),
        };

        await MapAndPublishV2BatchAsync(batchData, activeTypes, result, config, cancellationToken);

        // Pump-mode state spans (auto/manual/sleep/exercise/...) — the ONE thing with no SSV2 source
        // (confirmed by reverse-engineering the app: only aggregate mode % is exposed, never per-interval
        // spans). Keep a single slim v3 graph/data call requesting ONLY the pump-mode series, fed into the
        // existing mapper. Windowed to a few recent days on incremental syncs (modes don't change
        // retroactively; dedup absorbs overlap), full range on backfill. Additional to the basal-derived
        // state spans from MapAndPublishV2BatchAsync; degrades to none on failure.
        if (activeTypes.Contains(SyncDataType.StateSpans))
        {
            var modeTo = _timeMapper!.ToGlookoTime(DateTime.UtcNow).AddDays(1);
            var modeFrom = incremental ? modeTo.AddDays(-3) : from;
            var modeData = await FetchV3PumpModeGraphAsync(modeFrom, modeTo);
            if (modeData != null)
            {
                var modeSpans = _stateSpanMapper!.TransformV3PumpModeToStateSpans(modeData);
                if (modeSpans.Count > 0 && await PublishStateSpanDataAsync(modeSpans, config, cancellationToken))
                    result.ItemsSynced[SyncDataType.StateSpans] =
                        result.ItemsSynced.GetValueOrDefault(SyncDataType.StateSpans) + modeSpans.Count;
            }
        }

        // Pen injections — manual insulin logged via pen: injection_boluses → Bolus, injection_basals →
        // BasalInjection. The v3 path covers these via its gkInsulin* series; the windowed v2 batch path
        // does not. Critical for MDI users, who have no pump bolus/basal data at all.
        if (activeTypes.Contains(SyncDataType.Boluses) || activeTypes.Contains(SyncDataType.BasalInjections))
        {
            var injectionBoluses = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2InjectionBolusesPath,
                () => FetchSsv2Async<GlookoInjectionBolusPage, GlookoInjectionInsulin>(
                    GlookoConstants.Ssv2InjectionBolusesPath, p => p.InjectionBoluses, incremental, batchStart),
                new List<GlookoInjectionInsulin>());
            var injectionBasals = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2InjectionBasalsPath,
                () => FetchSsv2Async<GlookoInjectionBasalPage, GlookoInjectionInsulin>(
                    GlookoConstants.Ssv2InjectionBasalsPath, p => p.InjectionBasals, incremental, batchStart),
                new List<GlookoInjectionInsulin>());

            var (penBasals, penBoluses) = _v4TreatmentMapper!.MapSsv2InjectionInsulin(injectionBasals, injectionBoluses);

            await PublishRecordTypeAsync(result, SyncDataType.Boluses, activeTypes,
                penBoluses, PublishBolusDataAsync, config, cancellationToken);
            await PublishRecordTypeAsync(result, SyncDataType.BasalInjections, activeTypes,
                penBasals, PublishBasalInjectionDataAsync, config, cancellationToken);
        }

        // App-logged insulin doses — cgm/insulin_events: doses logged in the app by CGM-only/MDI users,
        // not pump-delivered. "fast_acting" → rapid Bolus, "long_acting"/"intermediate" → BasalInjection.
        // Distinct from the pen-injection feeds above (which carry a product name); this feed has none, so
        // DIA/peak is resolved by category.
        if (activeTypes.Contains(SyncDataType.Boluses) || activeTypes.Contains(SyncDataType.BasalInjections))
        {
            var insulinEvents = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2InsulinEventsPath,
                () => FetchSsv2Async<GlookoInsulinEventPage, GlookoSsv2InsulinEvent>(
                    GlookoConstants.Ssv2InsulinEventsPath, p => p.InsulinEvents, incremental, batchStart),
                new List<GlookoSsv2InsulinEvent>());

            var (eventBasals, eventBoluses) = _v4TreatmentMapper!.MapSsv2InsulinEvents(insulinEvents);

            await PublishRecordTypeAsync(result, SyncDataType.Boluses, activeTypes,
                eventBoluses, PublishBolusDataAsync, config, cancellationToken);
            await PublishRecordTypeAsync(result, SyncDataType.BasalInjections, activeTypes,
                eventBasals, PublishBasalInjectionDataAsync, config, cancellationToken);
        }

        // Extended/dual-wave boluses — square (all-extended) or dual (immediate + extended) deliveries
        // with a duration. Net-new vs the windowed path and the v3 graph (no extended-bolus series).
        if (activeTypes.Contains(SyncDataType.Boluses))
        {
            var extended = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2ExtendedBolusesPath,
                () => FetchSsv2Async<GlookoExtendedBolusPage, GlookoExtendedBolus>(
                    GlookoConstants.Ssv2ExtendedBolusesPath, p => p.ExtendedBoluses, incremental, batchStart),
                new List<GlookoExtendedBolus>());
            var extendedBoluses = _v4TreatmentMapper!.MapSsv2ExtendedBoluses(extended);
            await PublishRecordTypeAsync(result, SyncDataType.Boluses, activeTypes,
                extendedBoluses, PublishBolusDataAsync, config, cancellationToken);
        }

        // Standalone carbs — app-logged carb entries not tied to a bolus (v3 carbAll equivalent),
        // additional to the carbs derived from bolus.carbsInput + foods in MapAndPublishV2BatchAsync
        // (PublishRecordTypeAsync accumulates the count).
        if (activeTypes.Contains(SyncDataType.CarbIntake))
        {
            var carbsEvents = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2CarbsEventsPath,
                () => FetchSsv2Async<GlookoCarbsEventPage, GlookoSsv2CarbsEvent>(
                    GlookoConstants.Ssv2CarbsEventsPath, p => p.CarbsEvents, incremental, batchStart),
                new List<GlookoSsv2CarbsEvent>());
            var standaloneCarbs = _v4TreatmentMapper!.MapSsv2CarbsEvents(carbsEvents);
            await PublishRecordTypeAsync(result, SyncDataType.CarbIntake, activeTypes,
                standaloneCarbs, PublishCarbIntakeDataAsync, config, cancellationToken);
        }

        // Notes — app-logged free-text notes (camelCase /api/v2/notes) → Note.
        if (activeTypes.Contains(SyncDataType.Notes))
        {
            var rawNotes = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2NotesPath,
                () => FetchSsv2Async<GlookoNotePage, GlookoSsv2Note>(
                    GlookoConstants.Ssv2NotesPath, p => p.Notes, incremental, batchStart),
                new List<GlookoSsv2Note>());
            var notes = _noteMapper!.MapSsv2Notes(rawNotes);
            await PublishRecordTypeAsync(result, SyncDataType.Notes, activeTypes,
                notes, PublishNoteDataAsync, config, cancellationToken);
        }

        // Activities — two app-logged exercise sources mapped to Activity: exercises (seconds duration,
        // numeric intensity) and cgm/exercise_events (minutes duration, string intensity). Both normalize
        // to minutes. PublishRecordTypeAsync accumulates the count across the two sources.
        if (activeTypes.Contains(SyncDataType.Activity))
        {
            var rawExercises = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2ExercisesPath,
                () => FetchSsv2Async<GlookoExercisePage, GlookoSsv2Exercise>(
                    GlookoConstants.Ssv2ExercisesPath, p => p.Exercises, incremental, batchStart),
                new List<GlookoSsv2Exercise>());
            var exerciseActivities = _activityMapper!.MapSsv2Exercises(rawExercises);
            await PublishRecordTypeAsync(result, SyncDataType.Activity, activeTypes,
                exerciseActivities, PublishActivityDataAsync, config, cancellationToken);

            var rawExerciseEvents = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2ExerciseEventsPath,
                () => FetchSsv2Async<GlookoExerciseEventPage, GlookoSsv2ExerciseEvent>(
                    GlookoConstants.Ssv2ExerciseEventsPath, p => p.ExerciseEvents, incremental, batchStart),
                new List<GlookoSsv2ExerciseEvent>());
            var exerciseEventActivities = _activityMapper!.MapSsv2ExerciseEvents(rawExerciseEvents);
            await PublishRecordTypeAsync(result, SyncDataType.Activity, activeTypes,
                exerciseEventActivities, PublishActivityDataAsync, config, cancellationToken);
        }

        // Device events — granular pumps/events feed (reservoir/site/cannula changes) plus pump alarms
        // (→ system events). Net-new for SSV2 vs the windowed batch path; the v3 path derives both from
        // its graph series. Both are reported under the DeviceEvents count, matching the v3 path.
        if (activeTypes.Contains(SyncDataType.DeviceEvents))
        {
            var deviceEventCount = 0;

            var pumpEvents = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2PumpEventsPath,
                () => FetchSsv2Async<GlookoPumpEventPage, GlookoPumpEvent>(
                    GlookoConstants.Ssv2PumpEventsPath, p => p.Events, incremental, batchStart),
                new List<GlookoPumpEvent>());
            var deviceEvents = _pumpEventMapper!.TransformPumpEventsToDeviceEvents(pumpEvents);
            if (deviceEvents.Count > 0 && await PublishDeviceEventDataAsync(deviceEvents, config, cancellationToken))
                deviceEventCount += deviceEvents.Count;

            var alarms = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2AlarmsPath,
                () => FetchSsv2Async<GlookoSsv2AlarmPage, GlookoSsv2Alarm>(
                    GlookoConstants.Ssv2AlarmsPath, p => p.Alarms, incremental, batchStart),
                new List<GlookoSsv2Alarm>());
            var systemEvents = _systemEventMapper!.TransformSsv2AlarmsToSystemEvents(alarms);
            if (systemEvents.Count > 0 && await PublishSystemEventDataAsync(systemEvents, config, cancellationToken))
                deviceEventCount += systemEvents.Count;

            if (deviceEventCount > 0)
                result.ItemsSynced[SyncDataType.DeviceEvents] = deviceEventCount;

            // Patient hardware inventory — the pumps / cgm_devices feeds map to PatientDevice (the user's
            // pump + CGM). Gated under DeviceEvents as the closest existing device gate (there is no
            // SyncDataType for hardware inventory). Upserted via IDevicePublisher.PublishPatientDevicesAsync
            // keyed on the mapper's deterministic Id, so re-syncs update in place.
            var pumpDevices = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2PumpsPath,
                () => FetchSsv2Async<GlookoPumpDevicePage, GlookoSsv2Device>(
                    GlookoConstants.Ssv2PumpsPath, p => p.Pumps, incremental, batchStart),
                new List<GlookoSsv2Device>());
            var cgmDevices = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2CgmDevicesPath,
                () => FetchSsv2Async<GlookoCgmDevicePage, GlookoSsv2Device>(
                    GlookoConstants.Ssv2CgmDevicesPath, p => p.CgmDevices, incremental, batchStart),
                new List<GlookoSsv2Device>());

            var patientDevices = _deviceMapper!.TransformPumpsToPatientDevices(pumpDevices);
            patientDevices.AddRange(_deviceMapper.TransformCgmDevicesToPatientDevices(cgmDevices));
            if (patientDevices.Count > 0 && _connectorPublisher is { IsAvailable: true })
                await _connectorPublisher.Device.PublishPatientDevicesAsync(patientDevices, ConnectorSource, cancellationToken);
        }

        // Profiles — SSV2-native source from pumps/settings (basal/bolus programs), replacing the v3
        // devices_and_settings call the windowed paths use. The current snapshot becomes one Nocturne
        // Profile. Unlike the v3 mapper there are no profile state spans here: pumps/settings exposes only
        // the current program set, not a historical active-profile timeline.
        if (activeTypes.Contains(SyncDataType.Profiles))
        {
            var settings = await FetchSsv2SafelyAsync(
                GlookoConstants.Ssv2PumpSettingsPath,
                () => FetchSsv2Async<GlookoSsv2PumpSettingsPage, GlookoSsv2PumpSettings>(
                    GlookoConstants.Ssv2PumpSettingsPath, p => p.Settings, incremental, batchStart),
                new List<GlookoSsv2PumpSettings>());

            var profile = _settingsProfileMapper!.TransformSettingsToProfile(settings);
            if (profile != null
                && await PublishProfileDataAsync(new List<Profile> { profile }, config, cancellationToken))
            {
                result.ItemsSynced[SyncDataType.Profiles] = 1;
                _logger.LogInformation("[{ConnectorSource}] Published profile from SSV2 pump settings", ConnectorSource);
            }
        }
    }

    /// <summary>
    ///     Fetches one SSV2 batch resource and returns its records as an array, or an empty array if the
    ///     fetch fails (logged and skipped — see <see cref="FetchSsv2SafelyAsync{T}"/>).
    /// </summary>
    private Task<TRecord[]> FetchSsv2BatchResourceAsync<TPage, TRecord>(
        string resource, Func<TPage, TRecord[]?> selectRecords, bool incremental, DateTime? startDate)
        where TPage : GlookoSsv2Page
        => FetchSsv2SafelyAsync(
            resource,
            async () => (await FetchSsv2Async<TPage, TRecord>(resource, selectRecords, incremental, startDate)).ToArray(),
            Array.Empty<TRecord>());

    /// <summary>
    ///     Runs an SSV2 fetch and returns its result, or — if it throws — logs a warning and returns
    ///     <paramref name="fallback"/> so one failing feed degrades to "no records this pass" instead of
    ///     aborting the whole sync. Mirrors the per-endpoint resilience of the windowed batch path.
    /// </summary>
    private async Task<T> FetchSsv2SafelyAsync<T>(string resource, Func<Task<T>> fetch, T fallback)
    {
        try
        {
            return await fetch();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[{ConnectorSource}] SSV2 fetch for {Resource} failed; skipping it and continuing with the rest of the sync",
                ConnectorSource, resource);
            return fallback;
        }
    }

    private static string ConstructSsv2Url(string resource, DateTime? startDate, string lastUpdatedAt, string lastGuid)
    {
        // sendSoftDeleted=false by design: downstream deletion propagation isn't built, so we neither
        // page through tombstones nor act on them. Consequence (same as the windowed path): a record
        // deleted at the source *after* it was already ingested is never removed here. Flipping this to
        // true is only safe once tombstone ingest + downstream soft-delete exists — tracked as a
        // separate SSV2 follow-up.
        var url = $"{resource}?lastUpdatedAt={lastUpdatedAt}"
                + $"&lastGuid={lastGuid}"
                + $"&limit={GlookoConstants.Ssv2PageSize}"
                + "&sendSoftDeleted=false&allDevicesFlag=true";

        if (startDate.HasValue)
            url += $"&startDate={startDate.Value:yyyy-MM-ddTHH:mm:ss.fffZ}";

        return url;
    }

    private string? _meterUnits;
    private string? _timezone;

    /// <summary>
    ///     Fetches user profile from v3 API to get meter units and the account's home timezone.
    /// </summary>
    public async Task<GlookoV3UsersResponse?> FetchV3UserProfileAsync()
    {
        try
        {
            EnsureAuthenticatedAndGetCode();

            var result = await FetchFromGlookoEndpoint(GlookoConstants.V3UsersPath);
            if (!result.HasValue) return null;

            var profile = JsonSerializer.Deserialize<GlookoV3UsersResponse>(result.Value.GetRawText());
            if (profile?.CurrentUser != null)
            {
                _meterUnits = profile.CurrentUser.MeterUnits;
                _timezone = profile.CurrentUser.Timezone;
                _logger.LogInformation("[{ConnectorSource}] User profile loaded. MeterUnits: {Units}, Timezone: {Timezone}",
                    ConnectorSource, _meterUnits, _timezone ?? "(none)");
            }

            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Glooko v3 user profile");
            return null;
        }
    }

    /// <summary>
    ///     Builds and installs the tenant's timezone timeline on the shared time mapper for this sync.
    ///     For V3 accounts it fetches the profile (capturing the home zone and meter units in one call)
    ///     and seeds the timeline origin from that zone on first sync. When no timezone service is wired
    ///     or the timeline is empty, conversion falls back to the legacy static offset.
    /// </summary>
    private async Task ConfigureTimezoneTimelineAsync(GlookoConnectorConfiguration config, CancellationToken cancellationToken)
    {
        if (_timezoneTimelineService is null || _timeMapper is null)
            return;

        try
        {
            if (config.UseV3Api && string.IsNullOrEmpty(_meterUnits))
                await FetchV3UserProfileAsync();

            if (!string.IsNullOrWhiteSpace(_timezone))
                await _timezoneTimelineService.EnsureOriginAsync(_timezone, cancellationToken);

            var resolver = await _timezoneTimelineService.GetResolverAsync(config.TimezoneOffset, cancellationToken);
            _timeMapper.UseTimeline(resolver);

            _logger.LogInformation(
                "[{ConnectorSource}] Timezone timeline configured (entries present: {HasEntries}, home zone: {Zone})",
                ConnectorSource, resolver.HasEntries, _timezone ?? "(none)");
        }
        catch (Exception ex)
        {
            // Never fail a sync over timeline setup — fall back to the static offset.
            _logger.LogWarning(ex, "[{ConnectorSource}] Failed to configure timezone timeline; using static offset", ConnectorSource);
        }
    }

    /// <summary>
    ///     Fetches data from v3 graph/data API — single call for all data types.
    /// </summary>
    public async Task<GlookoV3GraphResponse?> FetchV3GraphDataAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            var patientCode = EnsureAuthenticatedAndGetCode();
            if (patientCode == null) return null;

            if (string.IsNullOrEmpty(_meterUnits)) await FetchV3UserProfileAsync();

            var url = ConstructV3GraphUrl(fromDate, toDate);
            _logger.LogInformation("[{ConnectorSource}] Fetching v3 graph data from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                ConnectorSource, fromDate, toDate);

            var result = await FetchFromGlookoEndpointWithRetry(url);
            if (!result.HasValue) return null;

            var graphData = JsonSerializer.Deserialize<GlookoV3GraphResponse>(result.Value.GetRawText());

            if (graphData?.Series != null)
            {
                var s = graphData.Series;
                _logger.LogInformation(
                    "[{ConnectorSource}] Fetched v3 graph data: "
                    + "Cgm={Cgm}, Bg={Bg}, "
                    + "DeliveredBolus={DeliveredBolus}, AutomaticBolus={AutoBolus}, InjectionBolus={InjectionBolus}, "
                    + "GkInsulinBasal={GkBasal}, GkInsulinBolus={GkBolus}, "
                    + "CarbAll={Carbs}, "
                    + "ScheduledBasal={SchedBasal}, TemporaryBasal={TempBasal}, SuspendBasal={Suspend}, LgsPlgs={LgsPlgs}, "
                    + "PumpAlarm={Alarms}, ReservoirChange={Reservoir}, SetSiteChange={SetSite}, ProfileChange={Profile}",
                    ConnectorSource,
                    (s.CgmHigh?.Length ?? 0) + (s.CgmNormal?.Length ?? 0) + (s.CgmLow?.Length ?? 0),
                    (s.BgHigh?.Length ?? 0) + (s.BgNormal?.Length ?? 0) + (s.BgLow?.Length ?? 0),
                    s.DeliveredBolus?.Length ?? 0,
                    s.AutomaticBolus?.Length ?? 0,
                    s.InjectionBolus?.Length ?? 0,
                    s.GkInsulinBasal?.Length ?? 0,
                    s.GkInsulinBolus?.Length ?? 0,
                    s.CarbAll?.Length ?? 0,
                    s.ScheduledBasal?.Length ?? 0,
                    s.TemporaryBasal?.Length ?? 0,
                    s.SuspendBasal?.Length ?? 0,
                    s.LgsPlgs?.Length ?? 0,
                    s.PumpAlarm?.Length ?? 0,
                    s.ReservoirChange?.Length ?? 0,
                    s.SetSiteChange?.Length ?? 0,
                    s.ProfileChange?.Length ?? 0);
            }

            return graphData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Glooko v3 graph data");
            return null;
        }
    }

    /// <summary>
    ///     Fetches pump device settings from the v3 devices_and_settings API.
    /// </summary>
    public async Task<GlookoV3DeviceSettingsResponse?> FetchV3DeviceSettingsAsync()
    {
        try
        {
            var patientCode = EnsureAuthenticatedAndGetCode();
            if (patientCode == null) return null;

            var url = $"{GlookoConstants.V3DeviceSettingsPath}?patient={patientCode}";
            _logger.LogInformation("[{ConnectorSource}] Fetching device settings from v3 API", ConnectorSource);

            var result = await FetchFromGlookoEndpointWithRetry(url);
            if (!result.HasValue) return null;

            var settings = JsonSerializer.Deserialize<GlookoV3DeviceSettingsResponse>(result.Value.GetRawText());

            var pumpCount = settings?.DeviceSettings?.Pumps?.Count ?? 0;
            var snapshotCount = settings?.DeviceSettings?.Pumps?.Values.Sum(p => p.Count) ?? 0;

            _logger.LogInformation("[{ConnectorSource}] Fetched device settings: {PumpCount} pumps, {SnapshotCount} settings snapshots",
                ConnectorSource, pumpCount, snapshotCount);

            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Glooko v3 device settings");
            return null;
        }
    }

    /// <summary>
    ///     Fetches rich history data from the v3 users/summary/histories API.
    ///     Contains meals with per-food nutritional data, medications, exercises, etc.
    /// </summary>
    public async Task<GlookoV3HistoriesResponse?> FetchV3HistoriesAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            var patientCode = EnsureAuthenticatedAndGetCode();
            if (patientCode == null) return null;

            var url = $"{GlookoConstants.V3HistoriesPath}?patient={patientCode}"
                    + $"&startDate={fromDate:yyyy-MM-ddTHH:mm:ss.fffZ}"
                    + $"&endDate={toDate:yyyy-MM-ddTHH:mm:ss.fffZ}";

            _logger.LogInformation("[{ConnectorSource}] Fetching v3 histories from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                ConnectorSource, fromDate, toDate);

            var result = await FetchFromGlookoEndpointWithRetry(url);
            if (!result.HasValue) return null;

            var historiesData = JsonSerializer.Deserialize<GlookoV3HistoriesResponse>(result.Value.GetRawText());

            var entryCount = historiesData?.Histories?.Length ?? 0;
            var meals = GlookoV4TreatmentMapper.ExtractMeals(historiesData!).ToList();
            var mealCount = meals.Count;
            var foodCount = meals.Sum(m => m.Foods?.Length ?? 0);
            var mealsWithCarbs = meals.Count(m => (m.Carbs ?? 0) > 0);

            _logger.LogInformation(
                "[{ConnectorSource}] Fetched v3 histories: {EntryCount} entries, {MealCount} meals ({MealsWithCarbs} with carbs), {FoodCount} food items",
                ConnectorSource, entryCount, mealCount, mealsWithCarbs, foodCount);

            return historiesData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Glooko v3 histories");
            return null;
        }
    }

    // ── Progress reporting ──────────────────────────────────────────────

    private Task ReportMessageAsync(
        ISyncProgressReporter? reporter,
        SyncMessageType messageType,
        Dictionary<string, string>? messageParams,
        CancellationToken ct)
    {
        if (reporter == null) return Task.CompletedTask;
        return reporter.ReportProgressAsync(new SyncProgressEvent
        {
            ConnectorId = ConnectorSource,
            ConnectorName = ServiceName,
            Phase = SyncPhase.Syncing,
            MessageType = messageType,
            MessageParams = messageParams,
        }, ct);
    }
}
