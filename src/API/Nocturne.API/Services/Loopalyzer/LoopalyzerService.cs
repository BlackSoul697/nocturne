using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts.Glucose;
using Nocturne.Core.Contracts.Loopalyzer;
using Nocturne.Core.Contracts.Profiles.Resolvers;
using Nocturne.Core.Contracts.Treatments;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Loopalyzer;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.Loopalyzer;

public sealed class LoopalyzerService : ILoopalyzerService
{
    private readonly LoopalyzerOptions _options;
    private readonly IEntryService _entryService;
    private readonly ITherapyTimelineResolver _therapyTimeline;
    private readonly ITempBasalRepository _tempBasalRepo;
    private readonly IApsSnapshotRepository _apsRepo;
    private readonly ITreatmentService _treatmentService;
    private readonly IIobService _iobService;

    public LoopalyzerService(
        IOptions<LoopalyzerOptions> options,
        IEntryService entryService,
        ITherapyTimelineResolver therapyTimeline,
        ITempBasalRepository tempBasalRepo,
        IApsSnapshotRepository apsRepo,
        ITreatmentService treatmentService,
        IIobService iobService)
    {
        _options = options.Value;
        _entryService = entryService;
        _therapyTimeline = therapyTimeline;
        _tempBasalRepo = tempBasalRepo;
        _apsRepo = apsRepo;
        _treatmentService = treatmentService;
        _iobService = iobService;
    }

    /// <summary>
    /// Result of a per-day IOB binning operation: the binned (and interpolated) array
    /// plus a flag indicating whether any APS-snapshot data contributed.
    /// </summary>
    internal sealed record IobBinResult(double?[] Bins, bool HasApsData);

    /// <summary>
    /// Bin IOB for a single day. ApsSnapshot.IOB takes precedence per tick; falls back
    /// to per-tick treatment IOB calculation. Short null gaps are interpolated.
    /// </summary>
    internal async Task<IobBinResult> BinIobAsync(DateOnly day, TimeZoneInfo tz, CancellationToken ct)
    {
        var (fromMills, toMills) = LocalDayWindowMillsUtc(day, tz);
        var apsToleranceMs = (long)TimeSpan.FromMinutes(5).TotalMilliseconds;

        // APS snapshots within the day plus a small forward tolerance.
        var apsSnapshots = (await _apsRepo.GetAsync(
            from: DateTimeOffset.FromUnixTimeMilliseconds(fromMills - (long)TimeSpan.FromMinutes(5).TotalMilliseconds).UtcDateTime,
            to: DateTimeOffset.FromUnixTimeMilliseconds(toMills).UtcDateTime,
            device: null, source: null,
            limit: MaxRecordsPerDay, offset: 0, descending: false, ct: ct))
            .Where(s => s.Iob.HasValue)
            .OrderBy(s => s.Timestamp)
            .ToList();

        // Treatments with 8h prior padding so IOB at 00:00 has lookback.
        var treatStartUtc = DateTimeOffset.FromUnixTimeMilliseconds(fromMills - (long)TimeSpan.FromHours(8).TotalMilliseconds).UtcDateTime;
        var treatEndUtc = DateTimeOffset.FromUnixTimeMilliseconds(toMills).UtcDateTime;
        var treatments = (await _treatmentService.GetTreatmentsAsync(count: MaxRecordsPerDay, skip: 0, cancellationToken: ct))
            ?.Where(t => t.Mills >= new DateTimeOffset(treatStartUtc, TimeSpan.Zero).ToUnixTimeMilliseconds()
                      && t.Mills <= new DateTimeOffset(treatEndUtc, TimeSpan.Zero).ToUnixTimeMilliseconds())
            .ToList()
            ?? new List<Treatment>();

        var timeline = await _therapyTimeline.BuildAsync(fromMills - (long)TimeSpan.FromHours(8).TotalMilliseconds, toMills, ct: ct);
        var hasApsData = false;

        var bins = LoopalyzerBinning.BinByMidpoint(day, tz, mills =>
        {
            var aps = NearestAps(apsSnapshots, mills, apsToleranceMs);
            if (aps?.Iob is double iob)
            {
                hasApsData = true;
                return iob;
            }

            var snapshot = timeline.SnapshotAt(mills);
            var iobResult = _iobService.FromTreatments(treatments, mills, snapshot);
            return iobResult.Iob > 0 ? iobResult.Iob : (double?)null;
        });

        BinInterpolator.Interpolate(bins, _options.RisingInterpolationGap, _options.FallingInterpolationGap, _options.InterpolationRatio);
        return new IobBinResult(bins, hasApsData);
    }

    private static ApsSnapshot? NearestAps(IReadOnlyList<ApsSnapshot> sortedAsc, long mills, long toleranceMs)
    {
        // Binary-search-like linear scan would suffice for typical day sizes; APS data is small.
        ApsSnapshot? best = null;
        var bestDelta = long.MaxValue;
        foreach (var s in sortedAsc)
        {
            var t = new DateTimeOffset(s.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var delta = Math.Abs(t - mills);
            if (delta <= toleranceMs && delta < bestDelta)
            {
                best = s;
                bestDelta = delta;
            }
            if (t > mills + toleranceMs)
                break;
        }
        return best;
    }

    private const int MaxTempLookbackHours = 24;
    private const int MaxRecordsPerDay = 5000;

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
