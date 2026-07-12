using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts.Glucose;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Contracts.Sleep;
using Nocturne.Core.Contracts.Treatments;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;
using Nocturne.Services.Demo.Configuration;
using Nocturne.Services.Demo.Services;

namespace Nocturne.API.Services.DevOnly;

/// <summary>
/// Populates a tenant with realistic sample data using the demo service's oref
/// pharmacokinetic generator, written through the normal ingestion services
/// (<see cref="IEntryService"/> / <see cref="ITreatmentService"/>) so device
/// attribution, the v4 canonical glucose stream, and RLS tenant context are
/// handled exactly like production writes. Development-only: consumed by the
/// dev-only admin endpoints, which do not exist outside Development.
/// </summary>
public class DevSampleDataService
{
    private readonly ITenantAccessor _tenantAccessor;
    private readonly NocturneDbContext _db;
    private readonly IEntryService _entryService;
    private readonly ITreatmentService _treatmentService;
    private readonly ISleepService _sleepService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DevSampleDataService> _logger;

    private const int BatchSize = 500;
    private const int MaxDays = 90;

    /// <summary>
    /// The generator stamps records with DataSources.DemoService, which
    /// DataSources.IsEphemeral hides from every non-demo tenant's reads —
    /// seeded data must carry a non-ephemeral source to be visible.
    /// </summary>
    private const string SampleDataSource = "dev-sample";

    public DevSampleDataService(
        ITenantAccessor tenantAccessor,
        NocturneDbContext db,
        IEntryService entryService,
        ITreatmentService treatmentService,
        ISleepService sleepService,
        ILoggerFactory loggerFactory,
        ILogger<DevSampleDataService> logger)
    {
        _tenantAccessor = tenantAccessor;
        _db = db;
        _entryService = entryService;
        _treatmentService = treatmentService;
        _sleepService = sleepService;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Generates and persists <paramref name="days"/> days of CGM entries,
    /// treatments, and one overnight sleep session per night for the tenant.
    /// Returns the persisted record counts.
    /// </summary>
    public async Task<(int Entries, int Treatments, int SleepSessions)> SeedAsync(
        TenantContext tenant, int days, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 1, MaxDays);

        var config = new DemoModeConfiguration { BackfillDays = days };
        var generator = new DemoDataGenerator(
            Options.Create(config),
            _loggerFactory.CreateLogger<DemoDataGenerator>(),
            _loggerFactory);

        // Ingestion services resolve the tenant through ITenantAccessor for
        // factory-created contexts, and through the request-scoped context's
        // TenantId (normally pinned by tenant resolution middleware, which
        // dev-only routes bypass) for entity stamping and the RLS GUC.
        _tenantAccessor.SetTenant(tenant);
        _db.TenantId = tenant.TenantId;

        var entries = generator.GenerateHistoricalEntries()
            .Select(e =>
            {
                e.DataSource = SampleDataSource;
                return e;
            });

        var entryCount = 0;
        foreach (var batch in entries.Chunk(BatchSize))
        {
            await _entryService.CreateEntriesAsync(batch, ct);
            entryCount += batch.Length;
        }

        // "Scheduled Basal" is a demo-service event type the treatment
        // decomposer doesn't recognize — it decomposes to nothing and logs a
        // warning per record.
        var treatments = generator.GenerateHistoricalTreatments()
            .Where(t => t.EventType != "Scheduled Basal")
            .Select(t =>
            {
                t.DataSource = SampleDataSource;
                return t;
            });

        var treatmentCount = 0;
        foreach (var batch in treatments.Chunk(BatchSize))
        {
            await _treatmentService.CreateTreatmentsAsync(batch, ct);
            treatmentCount += batch.Length;
        }

        var sleepCount = await SeedSleepAsync(days, ct);

        _logger.LogInformation(
            "Seeded {Entries} entries, {Treatments} treatments, {Sleep} sleep sessions ({Days} days) into tenant {Slug}",
            entryCount, treatmentCount, sleepCount, days, tenant.Slug);

        return (entryCount, treatmentCount, sleepCount);
    }

    /// <summary>
    /// Generates one overnight <see cref="SleepSession"/> per night for the last
    /// <paramref name="days"/> days — realistic stage cycles (deep concentrated
    /// early, REM later), per-stage biometric samples, and derived summary
    /// fields — written through <see cref="ISleepService"/> so RLS, entity
    /// mapping, and (Source, OriginalId) dedup on re-seed are handled like a
    /// connector import. Deterministic per tenant so re-seeding is idempotent.
    /// </summary>
    private async Task<int> SeedSleepAsync(int days, CancellationToken ct)
    {
        var rng = new Random(unchecked(_db.TenantId.GetHashCode()));
        // Anchor to local midnight so bedtime lands at night in the viewer's
        // timezone (dev runs the API and browser on the same machine); stored as
        // UTC. A UTC anchor would render mid-morning for non-UTC viewers.
        var localToday = DateTime.Now.Date;
        var count = 0;

        for (var d = 1; d <= days; d++)
        {
            // Lights-out on the evening of (today - d), 22:00–23:30 local time.
            var bedtimeLocal = localToday.AddDays(-d).AddHours(22).AddMinutes(rng.Next(0, 90));
            await _sleepService.UpsertSessionAsync(
                BuildSleepSession(bedtimeLocal.ToUniversalTime(), rng), ct);
            count++;
        }

        return count;
    }

    private static SleepSession BuildSleepSession(DateTime bedtime, Random rng)
    {
        var stages = new List<SleepStageInterval>();
        long deepMs = 0, lightMs = 0, remMs = 0, awakeMs = 0;
        var cursor = bedtime;
        var ordinal = 0;

        void AddStage(SleepStageType stage, int minutes)
        {
            if (minutes <= 0)
                return;
            var end = cursor.AddMinutes(minutes);
            stages.Add(new SleepStageInterval
            {
                StartTime = cursor,
                EndTime = end,
                Stage = stage,
                Ordinal = ordinal++,
            });
            var ms = (long)minutes * 60_000;
            switch (stage)
            {
                case SleepStageType.Deep: deepMs += ms; break;
                case SleepStageType.Rem: remMs += ms; break;
                case SleepStageType.Light or SleepStageType.Asleep: lightMs += ms; break;
                default: awakeMs += ms; break;
            }
            cursor = end;
        }

        var latencyMinutes = rng.Next(5, 20);
        AddStage(SleepStageType.Awake, latencyMinutes);

        // 5–6 ~90-minute cycles → a realistic ~7–8 h night.
        var cycles = rng.Next(5, 7);
        for (var c = 0; c < cycles; c++)
        {
            var progress = c / (double)cycles;
            AddStage(SleepStageType.Light, rng.Next(15, 30));
            AddStage(SleepStageType.Deep, (int)(rng.Next(20, 45) * (1.0 - 0.6 * progress))); // deep fades through the night
            AddStage(SleepStageType.Light, rng.Next(12, 22));
            AddStage(SleepStageType.Rem, (int)(rng.Next(12, 30) * (0.5 + 0.8 * progress))); // REM lengthens toward morning
            if (c < cycles - 1 && rng.NextDouble() < 0.5)
                AddStage(SleepStageType.Awake, rng.Next(2, 8));
        }
        AddStage(SleepStageType.Awake, rng.Next(2, 10));

        var start = bedtime;
        var end = cursor;
        var durationMs = (long)(end - start).TotalMilliseconds;
        var totalSleepMs = deepMs + lightMs + remMs;

        var samples = new List<SleepBiometricSample>();
        for (var t = start.AddMinutes(10); t < end; t = t.AddMinutes(rng.Next(18, 26)))
        {
            var stage = stages.FirstOrDefault(s => s.StartTime <= t && s.EndTime > t)?.Stage
                ?? SleepStageType.Light;
            float hr = stage switch
            {
                SleepStageType.Deep => rng.Next(48, 54),
                SleepStageType.Rem => rng.Next(56, 64),
                SleepStageType.Awake or SleepStageType.AwakeInBed => rng.Next(60, 70),
                _ => rng.Next(52, 60),
            };
            samples.Add(new SleepBiometricSample
            {
                Timestamp = t,
                HeartRate = hr,
                Hrv = rng.Next(40, 90),
                Spo2 = rng.Next(94, 99),
                RespirationRate = rng.Next(12, 17),
                Movement = (float)Math.Round(rng.NextDouble(), 2),
            });
        }

        var efficiency = durationMs > 0 ? (float)Math.Round(100.0 * totalSleepMs / durationMs, 1) : 0f;
        var restfulPct = totalSleepMs > 0 ? (deepMs + remMs) * 100.0 / totalSleepMs : 0;
        var score = (short)Math.Clamp((int)Math.Round(efficiency * 0.5 + restfulPct * 0.5), 50, 98);

        return new SleepSession
        {
            StartTime = start,
            EndTime = end,
            Type = SleepSessionType.Overnight,
            DetectionMethod = SleepDetectionMethod.Auto,
            Source = SleepSource.Oura,
            SourceDevice = "Oura Ring Gen3",
            SourceApp = SampleDataSource,
            IsMainSleep = true,
            DurationMs = durationMs,
            TotalSleepMs = totalSleepMs,
            TotalAwakeMs = awakeMs,
            DeepSleepMs = deepMs,
            LightSleepMs = lightMs,
            RemSleepMs = remMs,
            SleepLatencyMs = (long)latencyMinutes * 60_000,
            Efficiency = efficiency,
            RestlessPeriods = stages.Count(s => s.Stage == SleepStageType.Awake) - 1, // exclude sleep-onset latency
            SleepScore = score,
            AvgHeartRate = samples.Count > 0 ? (float)Math.Round(samples.Average(s => s.HeartRate!.Value), 1) : null,
            MinHeartRate = samples.Count > 0 ? samples.Min(s => s.HeartRate!.Value) : null,
            AvgHrv = samples.Count > 0 ? (float)Math.Round(samples.Average(s => s.Hrv!.Value), 1) : null,
            AvgBreathRate = samples.Count > 0 ? (float)Math.Round(samples.Average(s => s.RespirationRate!.Value), 1) : null,
            AvgSpo2 = samples.Count > 0 ? (float)Math.Round(samples.Average(s => s.Spo2!.Value), 1) : null,
            // Stable per-night key so re-seeding upserts rather than duplicates.
            OriginalId = $"dev-sample:sleep:{start:yyyy-MM-dd}",
            Stages = stages,
            BiometricSamples = samples,
        };
    }
}
