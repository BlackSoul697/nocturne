using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts.Glucose;
using Nocturne.Core.Contracts.Loopalyzer;
using Nocturne.Core.Contracts.Profiles.Resolvers;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.Loopalyzer;

namespace Nocturne.API.Services.Loopalyzer;

public sealed class LoopalyzerService : ILoopalyzerService
{
    private readonly LoopalyzerOptions _options;
    private readonly IEntryService _entryService;
    private readonly ITherapyTimelineResolver _therapyTimeline;
    private readonly ITempBasalRepository _tempBasalRepo;

    public LoopalyzerService(
        IOptions<LoopalyzerOptions> options,
        IEntryService entryService,
        ITherapyTimelineResolver therapyTimeline,
        ITempBasalRepository tempBasalRepo)
    {
        _options = options.Value;
        _entryService = entryService;
        _therapyTimeline = therapyTimeline;
        _tempBasalRepo = tempBasalRepo;
    }

    /// <summary>
    /// Bin temp basal deliveries for the local day. The fetch window is widened backward by
    /// <see cref="MaxTempLookbackHours"/> to catch temps that started the prior day and are
    /// still running into <paramref name="day"/>.
    /// </summary>
    internal async Task<double?[]> BinTempBasalAsync(DateOnly day, TimeZoneInfo tz, CancellationToken ct)
    {
        var (fromMills, toMills) = LocalDayWindowMillsUtc(day, tz);
        var lookback = TimeSpan.FromHours(MaxTempLookbackHours).TotalMilliseconds;
        var fetchFromUtc = DateTimeOffset.FromUnixTimeMilliseconds(fromMills - (long)lookback).UtcDateTime;
        var fetchToUtc = DateTimeOffset.FromUnixTimeMilliseconds(toMills).UtcDateTime;

        var temps = await _tempBasalRepo.GetAsync(
            from: fetchFromUtc,
            to: fetchToUtc,
            device: null,
            source: null,
            limit: 5000,
            offset: 0,
            descending: false,
            ct: ct);

        return LoopalyzerBinning.BinTempBasal(temps, day, tz);
    }

    private const int MaxTempLookbackHours = 24;

    /// <summary>
    /// Build the scheduled-basal bin array for a single patient-local day. The therapy timeline
    /// is built once for the local-day window so profile boundaries inside the day are honored
    /// without per-tick repository hits.
    /// </summary>
    internal async Task<double[]> BinScheduledBasalAsync(DateOnly day, TimeZoneInfo tz, CancellationToken ct)
    {
        var (fromMills, toMills) = LocalDayWindowMillsUtc(day, tz);
        var timeline = await _therapyTimeline.BuildAsync(fromMills, toMills, ct: ct);
        return LoopalyzerBinning.BinScheduledBasal(day, tz, mills => timeline.SnapshotAt(mills).BasalRateAt(mills));
    }

    /// <summary>
    /// Bin SGV entries for a single patient-local day into 288 5-minute slots.
    /// </summary>
    internal async Task<double?[]> BinSgvsAsync(DateOnly day, TimeZoneInfo tz, CancellationToken ct)
    {
        var (fromMills, toMills) = LocalDayWindowMillsUtc(day, tz);
        var findQuery = $"{{\"mills\":{{\"$gte\":{fromMills},\"$lt\":{toMills}}}}}";
        var entries = await _entryService.GetEntriesWithAdvancedFilterAsync(
            type: "sgv",
            count: 5000,
            skip: 0,
            findQuery: findQuery,
            dateString: null,
            reverseResults: false,
            cancellationToken: ct);

        return LoopalyzerBinning.BinSgvs(entries, day, tz);
    }

    /// <summary>
    /// Returns the UTC instants that bracket a patient-local day. A 25-hour DST fall-back
    /// day yields a 25-hour UTC window; a 23-hour spring-forward day yields a 23-hour window.
    /// </summary>
    internal static (long FromMills, long ToMills) LocalDayWindowMillsUtc(DateOnly day, TimeZoneInfo tz)
    {
        var localStart = new DateTime(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var localEnd = localStart.AddDays(1);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);
        return (
            new DateTimeOffset(fromUtc, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            new DateTimeOffset(toUtc, TimeSpan.Zero).ToUnixTimeMilliseconds());
    }

    public Task<LoopalyzerResponse> GetDataAsync(LoopalyzerRequest request, CancellationToken ct)
    {
        var (_, _) = ParseAndValidateRange(request);

        // Phase-1 stub: subsequent tasks fill in per-day binning, profiles, and DIA resolution.
        var response = new LoopalyzerResponse(
            Days: Array.Empty<LoopalyzerDay>(),
            Profiles: Array.Empty<LoopalyzerProfile>(),
            Timezone: "UTC",
            MostRecentDia: null,
            MostRecentBgLow: null,
            MostRecentBgHigh: null
        );
        return Task.FromResult(response);
    }

    public Task<LoopalyzerAvailability> GetAvailabilityAsync(CancellationToken ct)
        => Task.FromResult(new LoopalyzerAvailability(HasApsData: false, LatestApsAt: null));

    private (DateOnly From, DateOnly To) ParseAndValidateRange(LoopalyzerRequest request)
    {
        if (!DateOnly.TryParseExact(request.From, "yyyy-MM-dd", out var from))
            throw new ValidationException("From must be YYYY-MM-DD");
        if (!DateOnly.TryParseExact(request.To, "yyyy-MM-dd", out var to))
            throw new ValidationException("To must be YYYY-MM-DD");
        if (to < from)
            throw new ValidationException("To must be on or after From");
        if (to.DayNumber - from.DayNumber + 1 > _options.MaxRangeDays)
            throw new ValidationException($"Range cannot exceed {_options.MaxRangeDays} days");
        return (from, to);
    }
}
