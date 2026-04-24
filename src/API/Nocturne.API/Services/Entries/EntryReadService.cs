using Nocturne.API.Services.Platform;
using Nocturne.Core.Constants;
using Nocturne.Core.Contracts.Entries;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Entries;
namespace Nocturne.API.Services.Entries;

/// <summary>
/// Read-only <see cref="IEntryStore"/> that queries V4 repositories exclusively and projects
/// results into legacy <see cref="Entry"/> shape via <see cref="EntryProjection"/>.
/// Replaces <see cref="DualPathEntryStore"/> once the legacy entries table is dropped.
/// </summary>
public class EntryReadService : IEntryStore
{
    private readonly ISensorGlucoseRepository _sgRepo;
    private readonly IMeterGlucoseRepository _mgRepo;
    private readonly ICalibrationRepository _calRepo;
    private readonly IDemoModeService _demoMode;
    private readonly ILogger<EntryReadService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="EntryReadService"/>.
    /// </summary>
    public EntryReadService(
        ISensorGlucoseRepository sgRepo,
        IMeterGlucoseRepository mgRepo,
        ICalibrationRepository calRepo,
        IDemoModeService demoMode,
        ILogger<EntryReadService> logger)
    {
        _sgRepo = sgRepo;
        _mgRepo = mgRepo;
        _calRepo = calRepo;
        _demoMode = demoMode;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Entry>> QueryAsync(EntryQuery query, CancellationToken ct = default)
    {
        var descending = !query.ReverseResults;
        var source = ResolveDemoSource();
        var (from, to) = ResolveTimeRange(query);

        return query.Type switch
        {
            "sgv" => await QuerySgvAsync(from, to, source, query.Count, query.Skip, descending, ct),
            "mbg" => await QueryMbgAsync(from, to, source, query.Count, query.Skip, descending, ct),
            "cal" => await QueryCalAsync(from, to, source, query.Count, query.Skip, descending, ct),
            null or "" => await QueryAllTypesAsync(from, to, source, query.Count, query.Skip, descending, ct),
            _ => [],
        };
    }

    /// <inheritdoc />
    public async Task<Entry?> GetCurrentAsync(CancellationToken ct = default)
    {
        var source = ResolveDemoSource();
        var results = await _sgRepo.GetAsync(
            from: null, to: null, device: null, source: source,
            limit: 1, offset: 0, descending: true, nativeOnly: false, ct: ct);

        var sg = results.FirstOrDefault();
        return sg is null ? null : EntryProjection.FromSensorGlucose(sg);
    }

    /// <inheritdoc />
    public async Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (Guid.TryParse(id, out var guid))
            return await GetByGuidAsync(guid, ct);

        return await GetByLegacyIdAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<Entry?> CheckDuplicateAsync(string? device, string type, double? sgv, long mills,
        int windowMinutes = 5, CancellationToken ct = default)
    {
        var windowMs = (long)windowMinutes * 60 * 1000;
        var from = DateTimeOffset.FromUnixTimeMilliseconds(mills - windowMs).UtcDateTime;
        var to = DateTimeOffset.FromUnixTimeMilliseconds(mills + windowMs).UtcDateTime;

        return type switch
        {
            "sgv" => await CheckSgvDuplicateAsync(device, sgv, from, to, ct),
            "mbg" => await CheckMbgDuplicateAsync(device, sgv, from, to, ct),
            "cal" => await CheckCalDuplicateAsync(device, from, to, ct),
            _ => null,
        };
    }

    #region Private — Query helpers

    private async Task<IReadOnlyList<Entry>> QuerySgvAsync(
        DateTime? from, DateTime? to, string? source, int count, int skip, bool descending, CancellationToken ct)
    {
        var results = await _sgRepo.GetAsync(from, to, device: null, source, count + skip, 0, descending, false, ct);
        return results.Skip(skip).Take(count).Select(EntryProjection.FromSensorGlucose).ToList();
    }

    private async Task<IReadOnlyList<Entry>> QueryMbgAsync(
        DateTime? from, DateTime? to, string? source, int count, int skip, bool descending, CancellationToken ct)
    {
        var results = await _mgRepo.GetAsync(from, to, device: null, source, count + skip, 0, descending, ct);
        return results.Skip(skip).Take(count).Select(EntryProjection.FromMeterGlucose).ToList();
    }

    private async Task<IReadOnlyList<Entry>> QueryCalAsync(
        DateTime? from, DateTime? to, string? source, int count, int skip, bool descending, CancellationToken ct)
    {
        var results = await _calRepo.GetAsync(from, to, device: null, source, count + skip, 0, descending, ct);
        return results.Skip(skip).Take(count).Select(EntryProjection.FromCalibration).ToList();
    }

    private async Task<IReadOnlyList<Entry>> QueryAllTypesAsync(
        DateTime? from, DateTime? to, string? source, int count, int skip, bool descending, CancellationToken ct)
    {
        // Fetch count+skip from each repo to ensure correct merge pagination
        var fetchCount = count + skip;

        // Sequential to avoid DbContext thread-safety issues with scoped lifetime
        var sgResults = await _sgRepo.GetAsync(from, to, device: null, source, fetchCount, 0, descending, false, ct);
        var mgResults = await _mgRepo.GetAsync(from, to, device: null, source, fetchCount, 0, descending, ct);
        var calResults = await _calRepo.GetAsync(from, to, device: null, source, fetchCount, 0, descending, ct);

        var entries = sgResults.Select(EntryProjection.FromSensorGlucose)
            .Concat(mgResults.Select(EntryProjection.FromMeterGlucose))
            .Concat(calResults.Select(EntryProjection.FromCalibration));

        var sorted = descending
            ? entries.OrderByDescending(e => e.Mills)
            : entries.OrderBy(e => e.Mills);

        return sorted.Skip(skip).Take(count).ToList();
    }

    #endregion

    #region Private — GetById helpers

    private async Task<Entry?> GetByGuidAsync(Guid id, CancellationToken ct)
    {
        var sg = await _sgRepo.GetByIdAsync(id, ct);
        if (sg is not null)
            return EntryProjection.FromSensorGlucose(sg);

        var mg = await _mgRepo.GetByIdAsync(id, ct);
        if (mg is not null)
            return EntryProjection.FromMeterGlucose(mg);

        var cal = await _calRepo.GetByIdAsync(id, ct);
        if (cal is not null)
            return EntryProjection.FromCalibration(cal);

        return null;
    }

    private async Task<Entry?> GetByLegacyIdAsync(string legacyId, CancellationToken ct)
    {
        var sg = await _sgRepo.GetByLegacyIdAsync(legacyId, ct);
        if (sg is not null)
            return EntryProjection.FromSensorGlucose(sg);

        var mg = await _mgRepo.GetByLegacyIdAsync(legacyId, ct);
        if (mg is not null)
            return EntryProjection.FromMeterGlucose(mg);

        var cal = await _calRepo.GetByLegacyIdAsync(legacyId, ct);
        if (cal is not null)
            return EntryProjection.FromCalibration(cal);

        return null;
    }

    #endregion

    #region Private — Duplicate check helpers

    private async Task<Entry?> CheckSgvDuplicateAsync(
        string? device, double? sgv, DateTime from, DateTime to, CancellationToken ct)
    {
        var results = await _sgRepo.GetAsync(from, to, device, source: null, limit: 100, offset: 0, descending: true, nativeOnly: false, ct: ct);
        var match = sgv.HasValue
            ? results.FirstOrDefault(r => Math.Abs(r.Mgdl - sgv.Value) < 0.01)
            : results.FirstOrDefault();
        return match is null ? null : EntryProjection.FromSensorGlucose(match);
    }

    private async Task<Entry?> CheckMbgDuplicateAsync(
        string? device, double? mbg, DateTime from, DateTime to, CancellationToken ct)
    {
        var results = await _mgRepo.GetAsync(from, to, device, source: null, limit: 100, offset: 0, descending: true, ct: ct);
        var match = mbg.HasValue
            ? results.FirstOrDefault(r => Math.Abs(r.Mgdl - mbg.Value) < 0.01)
            : results.FirstOrDefault();
        return match is null ? null : EntryProjection.FromMeterGlucose(match);
    }

    private async Task<Entry?> CheckCalDuplicateAsync(
        string? device, DateTime from, DateTime to, CancellationToken ct)
    {
        var results = await _calRepo.GetAsync(from, to, device, source: null, limit: 100, offset: 0, descending: true, ct: ct);
        var match = results.FirstOrDefault();
        return match is null ? null : EntryProjection.FromCalibration(match);
    }

    #endregion

    #region Private — Filter resolution

    private string? ResolveDemoSource()
    {
        return _demoMode.IsEnabled ? DataSources.DemoService : null;
    }

    private static (DateTime? From, DateTime? To) ResolveTimeRange(EntryQuery query)
    {
        DateTime? from = null;
        DateTime? to = null;

        // Parse time range from MongoDB-style find query
        var (fromMills, toMills) = EntryDomainLogic.ParseTimeRangeFromFind(query.Find);
        if (fromMills.HasValue)
            from = DateTimeOffset.FromUnixTimeMilliseconds(fromMills.Value).UtcDateTime;
        if (toMills.HasValue)
            to = DateTimeOffset.FromUnixTimeMilliseconds(toMills.Value).UtcDateTime;

        // Parse DateString if provided (overrides find-based range)
        if (query.DateString is not null && DateTime.TryParse(query.DateString, out var parsedDate))
        {
            from = parsedDate.ToUniversalTime();
            to = from.Value.AddDays(1);
        }

        return (from, to);
    }

    #endregion
}
