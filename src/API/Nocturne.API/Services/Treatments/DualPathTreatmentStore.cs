using System.Text.Json;
using Nocturne.Core.Contracts.Treatments;
using Nocturne.Core.Contracts.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.API.Services.Treatments;

/// <summary>
/// Dual-path <see cref="ITreatmentStore"/> that handles reads by merging legacy <see cref="Treatment"/>
/// records with V4-projected <see cref="Nocturne.Core.Models.V4.TempBasal"/> records, and writes
/// that require dual-path awareness (create, update, delete with V4 decomposition).
/// </summary>
/// <remarks>
/// Pure pass-through writes (patch, bulk delete) bypass this store entirely and go directly to
/// <see cref="ITreatmentRepository"/> via <see cref="TreatmentService"/>.
/// On creation, new treatments are decomposed into V4 typed records via <see cref="IDecompositionPipeline"/>.
/// On deletion, the corresponding V4 record is cleaned up first via <see cref="ITreatmentDecomposer"/>.
/// </remarks>
/// <seealso cref="ITreatmentStore"/>
/// <seealso cref="IDecompositionPipeline"/>
/// <seealso cref="ITreatmentDecomposer"/>
/// <seealso cref="IV4ToLegacyProjectionService"/>
/// <seealso cref="TreatmentService"/>
public class DualPathTreatmentStore : ITreatmentStore
{
    private readonly ITreatmentRepository _treatmentRepository;
    private readonly ITreatmentDecomposer _decomposer;
    private readonly IDecompositionPipeline _pipeline;
    private readonly IV4ToLegacyProjectionService _projection;
    private readonly ITempBasalRepository _tempBasalRepo;
    private readonly ILogger<DualPathTreatmentStore> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="DualPathTreatmentStore"/>.
    /// </summary>
    /// <param name="treatmentRepository">The legacy treatment repository for data access and writes.</param>
    /// <param name="decomposer">Decomposer for cleaning up V4 records linked to a legacy treatment ID before deletion.</param>
    /// <param name="pipeline">Pipeline for projecting newly created treatments into V4 typed records.</param>
    /// <param name="projection">Service for projecting V4 temp basals back to the legacy <see cref="Treatment"/> shape.</param>
    /// <param name="tempBasalRepo">Repository for V4 <see cref="Nocturne.Core.Models.V4.TempBasal"/> records.</param>
    /// <param name="logger">The logger instance.</param>
    public DualPathTreatmentStore(
        ITreatmentRepository treatmentRepository,
        ITreatmentDecomposer decomposer,
        IDecompositionPipeline pipeline,
        IV4ToLegacyProjectionService projection,
        ITempBasalRepository tempBasalRepo,
        ILogger<DualPathTreatmentStore> logger)
    {
        _treatmentRepository = treatmentRepository;
        _decomposer = decomposer;
        _pipeline = pipeline;
        _projection = projection;
        _tempBasalRepo = tempBasalRepo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Treatment>> QueryAsync(TreatmentQuery query, CancellationToken ct)
    {
        var treatments = await _treatmentRepository.GetTreatmentsWithAdvancedFilterAsync(
            count: query.Count,
            skip: 0,
            findQuery: query.Find,
            reverseResults: false,
            cancellationToken: ct);

        var merged = await MergeWithTempBasalsAsync(treatments, query.Find, query.Count, query.Skip, ct);

        if (query.ReverseResults)
            return merged.OrderBy(t => t.Mills).ToList();

        return merged;
    }

    public async Task<Treatment?> GetByIdAsync(string id, CancellationToken ct)
    {
        var treatment = await _treatmentRepository.GetTreatmentByIdAsync(id, ct);
        if (treatment != null)
            return treatment;

        var tempBasal = await _tempBasalRepo.GetByLegacyIdAsync(id, ct);
        if (tempBasal == null && Guid.TryParse(id, out var guid))
            tempBasal = await _tempBasalRepo.GetByIdAsync(guid, ct);
        if (tempBasal != null)
            return TempBasalToTreatmentMapper.ToTreatment(tempBasal);

        return null;
    }

    public async Task<IReadOnlyList<Treatment>> GetModifiedSinceAsync(
        long lastModifiedMills, int limit, CancellationToken ct)
    {
        var treatments = await _treatmentRepository.GetTreatmentsModifiedSinceAsync(lastModifiedMills, limit, ct);

        var tempBasals = await _tempBasalRepo.GetAsync(
            from: DateTimeOffset.FromUnixTimeMilliseconds(lastModifiedMills).UtcDateTime,
            to: (DateTime?)null,
            device: null, source: null, limit: limit, offset: 0, descending: true, ct: ct);
        var tempBasalTreatments = TempBasalToTreatmentMapper.ToTreatments(tempBasals).ToList();

        return treatments
            .Concat(tempBasalTreatments)
            .OrderBy(t => t.SrvModified ?? t.Mills)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<Treatment>> CreateAsync(
        IReadOnlyList<Treatment> treatments, CancellationToken ct)
    {
        var regularTreatments = new List<Treatment>();
        var stateSpanTreatments = new List<Treatment>();
        var algorithmBolusTreatments = new List<Treatment>();

        foreach (var treatment in treatments)
        {
            if (TreatmentStateSpanMapper.IsTempBasalTreatment(treatment))
                stateSpanTreatments.Add(treatment);
            else if (treatment.IsBasalInsulin == true && treatment.Insulin > 0)
                algorithmBolusTreatments.Add(treatment);
            else
                regularTreatments.Add(treatment);
        }

        var results = new List<Treatment>();

        foreach (var t in stateSpanTreatments)
        {
            try
            {
                var result = await _decomposer.DecomposeAsync(t, ct);
                var created = result.CreatedRecords
                    .OfType<Core.Models.V4.TempBasal>()
                    .FirstOrDefault();
                if (created != null)
                    results.Add(TempBasalToTreatmentMapper.ToTreatment(created));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decompose temp basal treatment {Id}", t.Id);
            }
        }

        foreach (var t in algorithmBolusTreatments)
        {
            try
            {
                await _decomposer.DecomposeAsync(t, ct);
                results.Add(t);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decompose algorithm bolus treatment {Id}", t.Id);
            }
        }

        if (regularTreatments.Count > 0)
        {
            var created = await _treatmentRepository.CreateTreatmentsAsync(regularTreatments, ct);
            var createdList = created.ToList();
            await _pipeline.DecomposeAsync<Treatment>(createdList, ct);
            results.AddRange(createdList);
        }

        return results;
    }

    public async Task<Treatment?> UpdateAsync(string id, Treatment treatment, CancellationToken ct)
    {
        var existingTempBasal = await _tempBasalRepo.GetByLegacyIdAsync(id, ct);
        if (existingTempBasal == null && Guid.TryParse(id, out var guid))
            existingTempBasal = await _tempBasalRepo.GetByIdAsync(guid, ct);

        if (existingTempBasal != null)
        {
            try
            {
                treatment.Id = id;
                await _decomposer.DecomposeAsync(treatment, ct);
                var refreshed = await _tempBasalRepo.GetByIdAsync(existingTempBasal.Id, ct);
                return refreshed != null
                    ? TempBasalToTreatmentMapper.ToTreatment(refreshed)
                    : TempBasalToTreatmentMapper.ToTreatment(existingTempBasal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update TempBasal-backed treatment {Id}", id);
                return null;
            }
        }

        var updated = await _treatmentRepository.UpdateTreatmentAsync(id, treatment, ct);
        if (updated != null)
            await _pipeline.DecomposeAsync(updated, ct);

        return updated;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        await _pipeline.DeleteByLegacyIdAsync<Treatment>(id, ct);

        var existingTempBasal = await _tempBasalRepo.GetByLegacyIdAsync(id, ct);
        if (existingTempBasal == null && Guid.TryParse(id, out var guid))
            existingTempBasal = await _tempBasalRepo.GetByIdAsync(guid, ct);

        if (existingTempBasal != null)
        {
            try
            {
                await _tempBasalRepo.DeleteAsync(existingTempBasal.Id, ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete TempBasal record {Id}", existingTempBasal.Id);
                return false;
            }
        }

        return await _treatmentRepository.DeleteTreatmentAsync(id, ct);
    }

    public Task<long> CountAsync(string? find = null, CancellationToken ct = default)
    {
        throw new NotSupportedException("CountAsync is not supported on DualPathTreatmentStore. Use TreatmentReadService.");
    }

    private async Task<IReadOnlyList<Treatment>> MergeWithTempBasalsAsync(
        IEnumerable<Treatment> treatments, string? findQuery, int count, int skip,
        CancellationToken ct)
    {
        var (fromMills, toMills) = ParseTimeRangeFromFind(findQuery);

        var tempBasals = await _tempBasalRepo.GetAsync(
            from: fromMills.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(fromMills.Value).UtcDateTime : null,
            to: toMills.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(toMills.Value).UtcDateTime : null,
            device: null, source: null, limit: count, offset: 0, descending: true, ct: ct);

        var projectedTreatments = await _projection.GetProjectedTreatmentsAsync(
            fromMills, toMills, count, nativeOnly: true, ct: ct);

        var tempBasalTreatments = TempBasalToTreatmentMapper.ToTreatments(tempBasals).ToList();

        const long windowMillis = 30_000;
        var deduplicatedBasals = tempBasalTreatments
            .GroupBy(t => (WindowKey: t.Mills / windowMillis,
                           RateKey: t.Rate.HasValue ? Math.Round(t.Rate.Value * 20) / 20 : 0))
            .Select(g => g.OrderBy(t => t.Mills).First())
            .ToList();

        var legacyList = treatments.ToList();
        var legacyMillsSet = legacyList.Select(t => t.Mills).ToHashSet();
        var basalMillsSet = deduplicatedBasals.Select(t => t.Mills).ToHashSet();

        var filteredProjected = projectedTreatments
            .Where(p => !legacyMillsSet.Contains(p.Mills) && !basalMillsSet.Contains(p.Mills));

        return legacyList
            .Concat(deduplicatedBasals)
            .Concat(filteredProjected)
            .OrderByDescending(t => t.Mills)
            .Skip(skip)
            .Take(count)
            .ToList();
    }

    private static (long? from, long? to) ParseTimeRangeFromFind(string? find)
    {
        if (string.IsNullOrEmpty(find))
            return (null, null);

        long? from = null;
        long? to = null;

        try
        {
            using var doc = JsonDocument.Parse(find);
            foreach (var field in doc.RootElement.EnumerateObject())
            {
                if (field.Value.ValueKind != JsonValueKind.Object)
                    continue;
                foreach (var op in field.Value.EnumerateObject())
                {
                    if (op.Value.ValueKind != JsonValueKind.Number)
                        continue;
                    if (op.Name == "$gte" && op.Value.TryGetInt64(out var gte))
                        from = gte;
                    else if (op.Name == "$lte" && op.Value.TryGetInt64(out var lte))
                        to = lte;
                }
            }
        }
        catch (JsonException) { }

        return (from, to);
    }
}
