using System.Text.Json;
using Nocturne.Core.Contracts.Treatments;
using Nocturne.Core.Contracts.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Entries;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.API.Services.Treatments;

/// <summary>
/// V4-only <see cref="ITreatmentStore"/> that reads all treatments from V4 repositories
/// via the projection service and routes writes through the decomposer.
/// Replaces <see cref="DualPathTreatmentStore"/> once the legacy treatments table is dropped.
/// </summary>
public class TreatmentReadService : ITreatmentStore
{
    private readonly IV4ToLegacyProjectionService _projection;
    private readonly ITreatmentDecomposer _decomposer;
    private readonly IDecompositionPipeline _pipeline;
    private readonly ITempBasalRepository _tempBasalRepo;
    private readonly IBolusRepository _bolusRepo;
    private readonly ICarbIntakeRepository _carbIntakeRepo;
    private readonly IBGCheckRepository _bgCheckRepo;
    private readonly INoteRepository _noteRepo;
    private readonly IDeviceEventRepository _deviceEventRepo;
    private readonly IBolusCalculationRepository _bolusCalcRepo;
    private readonly ILogger<TreatmentReadService> _logger;

    public TreatmentReadService(
        IV4ToLegacyProjectionService projection,
        ITreatmentDecomposer decomposer,
        IDecompositionPipeline pipeline,
        ITempBasalRepository tempBasalRepo,
        IBolusRepository bolusRepo,
        ICarbIntakeRepository carbIntakeRepo,
        IBGCheckRepository bgCheckRepo,
        INoteRepository noteRepo,
        IDeviceEventRepository deviceEventRepo,
        IBolusCalculationRepository bolusCalcRepo,
        ILogger<TreatmentReadService> logger)
    {
        _projection = projection;
        _decomposer = decomposer;
        _pipeline = pipeline;
        _tempBasalRepo = tempBasalRepo;
        _bolusRepo = bolusRepo;
        _carbIntakeRepo = carbIntakeRepo;
        _bgCheckRepo = bgCheckRepo;
        _noteRepo = noteRepo;
        _deviceEventRepo = deviceEventRepo;
        _bolusCalcRepo = bolusCalcRepo;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Treatment>> QueryAsync(TreatmentQuery query, CancellationToken ct = default)
    {
        var (fromMills, toMills) = ParseTimeRangeFromFind(query.Find);

        var projected = await _projection.GetProjectedTreatmentsAsync(
            fromMills, toMills, query.Count + query.Skip, nativeOnly: false, ct: ct);

        var results = projected
            .OrderByDescending(t => t.Mills)
            .Skip(query.Skip)
            .Take(query.Count)
            .ToList();

        if (query.ReverseResults)
            return results.OrderBy(t => t.Mills).ToList();

        return results;
    }

    /// <inheritdoc />
    public async Task<Treatment?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (Guid.TryParse(id, out var guid))
            return await GetByGuidAsync(guid, ct);

        return await GetByLegacyIdAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Treatment>> GetModifiedSinceAsync(
        long lastModifiedMills, int limit, CancellationToken ct = default)
    {
        // Query all V4 repos and project — use a generous time window going forward from the cutoff
        var projected = await _projection.GetProjectedTreatmentsAsync(
            fromMills: lastModifiedMills, toMills: null, limit: limit, nativeOnly: false, ct: ct);

        return projected
            .OrderBy(t => t.SrvModified ?? t.Mills)
            .Take(limit)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Treatment>> CreateAsync(
        IReadOnlyList<Treatment> treatments, CancellationToken ct = default)
    {
        var results = new List<Treatment>();

        foreach (var treatment in treatments)
        {
            try
            {
                var result = await _decomposer.DecomposeAsync(treatment, ct);
                var tempBasal = result.CreatedRecords
                    .OfType<Core.Models.V4.TempBasal>()
                    .FirstOrDefault();
                if (tempBasal != null)
                    results.Add(TempBasalToTreatmentMapper.ToTreatment(tempBasal));
                else
                    results.Add(treatment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decompose treatment {Id}", treatment.Id);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<Treatment?> UpdateAsync(string id, Treatment treatment, CancellationToken ct = default)
    {
        var existing = await GetByIdAsync(id, ct);
        if (existing == null) return null;

        treatment.Id = id;
        try
        {
            await _decomposer.DecomposeAsync(treatment, ct);
            return await GetByIdAsync(id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update treatment {Id}", id);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var deleted = await _pipeline.DeleteByLegacyIdAsync<Treatment>(id, ct);

        // Also check TempBasal (not covered by the pipeline's LegacyId delete)
        var tempBasal = await _tempBasalRepo.GetByLegacyIdAsync(id, ct);
        if (tempBasal == null && Guid.TryParse(id, out var guid))
            tempBasal = await _tempBasalRepo.GetByIdAsync(guid, ct);

        if (tempBasal != null)
        {
            try
            {
                await _tempBasalRepo.DeleteAsync(tempBasal.Id, ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete TempBasal record {Id}", tempBasal.Id);
                return false;
            }
        }

        return deleted > 0;
    }

    #region Private — GetById helpers

    private async Task<Treatment?> GetByGuidAsync(Guid id, CancellationToken ct)
    {
        // Search across all V4 repos by ID
        var bolus = await _bolusRepo.GetByIdAsync(id, ct);
        if (bolus != null)
        {
            // Project back through the projection service for a single record
            var projected = await _projection.GetProjectedTreatmentsAsync(
                bolus.Mills, bolus.Mills, 1, nativeOnly: false, ct: ct);
            return projected.FirstOrDefault(t => t.Id == id.ToString());
        }

        var carbIntake = await _carbIntakeRepo.GetByIdAsync(id, ct);
        if (carbIntake != null)
        {
            var projected = await _projection.GetProjectedTreatmentsAsync(
                carbIntake.Mills, carbIntake.Mills, 1, nativeOnly: false, ct: ct);
            return projected.FirstOrDefault(t => t.Id == id.ToString());
        }

        var bgCheck = await _bgCheckRepo.GetByIdAsync(id, ct);
        if (bgCheck != null)
        {
            var projected = await _projection.GetProjectedTreatmentsAsync(
                bgCheck.Mills, bgCheck.Mills, 1, nativeOnly: false, ct: ct);
            return projected.FirstOrDefault(t => t.Id == id.ToString());
        }

        var note = await _noteRepo.GetByIdAsync(id, ct);
        if (note != null)
        {
            var projected = await _projection.GetProjectedTreatmentsAsync(
                note.Mills, note.Mills, 1, nativeOnly: false, ct: ct);
            return projected.FirstOrDefault(t => t.Id == id.ToString());
        }

        var deviceEvent = await _deviceEventRepo.GetByIdAsync(id, ct);
        if (deviceEvent != null)
        {
            var projected = await _projection.GetProjectedTreatmentsAsync(
                deviceEvent.Mills, deviceEvent.Mills, 1, nativeOnly: false, ct: ct);
            return projected.FirstOrDefault(t => t.Id == id.ToString());
        }

        var bolusCalc = await _bolusCalcRepo.GetByIdAsync(id, ct);
        if (bolusCalc != null)
        {
            var projected = await _projection.GetProjectedTreatmentsAsync(
                bolusCalc.Mills, bolusCalc.Mills, 1, nativeOnly: false, ct: ct);
            return projected.FirstOrDefault(t => t.Id == id.ToString());
        }

        var tempBasal = await _tempBasalRepo.GetByIdAsync(id, ct);
        if (tempBasal != null)
            return TempBasalToTreatmentMapper.ToTreatment(tempBasal);

        return null;
    }

    private async Task<Treatment?> GetByLegacyIdAsync(string legacyId, CancellationToken ct)
    {
        var bolus = await _bolusRepo.GetByLegacyIdAsync(legacyId, ct);
        if (bolus != null)
        {
            var projected = await _projection.GetProjectedTreatmentsAsync(
                bolus.Mills, bolus.Mills, 10, nativeOnly: false, ct: ct);
            return projected.FirstOrDefault(t => t.Id == bolus.Id.ToString());
        }

        var carbIntake = await _carbIntakeRepo.GetByLegacyIdAsync(legacyId, ct);
        if (carbIntake != null)
        {
            var projected = await _projection.GetProjectedTreatmentsAsync(
                carbIntake.Mills, carbIntake.Mills, 10, nativeOnly: false, ct: ct);
            return projected.FirstOrDefault(t => t.Id == carbIntake.Id.ToString());
        }

        var bgCheck = await _bgCheckRepo.GetByLegacyIdAsync(legacyId, ct);
        if (bgCheck != null)
        {
            var projected = await _projection.GetProjectedTreatmentsAsync(
                bgCheck.Mills, bgCheck.Mills, 10, nativeOnly: false, ct: ct);
            return projected.FirstOrDefault(t => t.Id == bgCheck.Id.ToString());
        }

        var noteRecord = await _noteRepo.GetByLegacyIdAsync(legacyId, ct);
        if (noteRecord != null)
        {
            var projected = await _projection.GetProjectedTreatmentsAsync(
                noteRecord.Mills, noteRecord.Mills, 10, nativeOnly: false, ct: ct);
            return projected.FirstOrDefault(t => t.Id == noteRecord.Id.ToString());
        }

        var deviceEvent = await _deviceEventRepo.GetByLegacyIdAsync(legacyId, ct);
        if (deviceEvent != null)
        {
            var projected = await _projection.GetProjectedTreatmentsAsync(
                deviceEvent.Mills, deviceEvent.Mills, 10, nativeOnly: false, ct: ct);
            return projected.FirstOrDefault(t => t.Id == deviceEvent.Id.ToString());
        }

        var bolusCalc = await _bolusCalcRepo.GetByLegacyIdAsync(legacyId, ct);
        if (bolusCalc != null)
        {
            var projected = await _projection.GetProjectedTreatmentsAsync(
                bolusCalc.Mills, bolusCalc.Mills, 10, nativeOnly: false, ct: ct);
            return projected.FirstOrDefault(t => t.Id == bolusCalc.Id.ToString());
        }

        var tempBasal = await _tempBasalRepo.GetByLegacyIdAsync(legacyId, ct);
        if (tempBasal != null)
            return TempBasalToTreatmentMapper.ToTreatment(tempBasal);

        return null;
    }

    #endregion

    #region Private — Find query parsing

    private static (long? From, long? To) ParseTimeRangeFromFind(string? find)
        => EntryDomainLogic.ParseTimeRangeFromFind(find);

    #endregion
}
