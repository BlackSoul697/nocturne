using System.Text.Json;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Entities.V4;

namespace Nocturne.Infrastructure.Data.Mappers.V4;

/// <summary>
/// Mapper for converting between BolusCalculation domain models and BolusCalculationEntity database entities
/// </summary>
public static class BolusCalculationMapper
{
    /// <summary>
    /// Convert domain model to database entity
    /// </summary>
    /// <param name="model">The domain model to convert.</param>
    /// <returns>A new instance of BolusCalculationEntity.</returns>
    public static BolusCalculationEntity ToEntity(BolusCalculation model)
    {
        return new BolusCalculationEntity
        {
            Id = model.Id == Guid.Empty ? Guid.CreateVersion7() : model.Id,
            Timestamp = model.Timestamp,
            UtcOffset = model.UtcOffset,
            Device = model.Device,
            App = model.App,
            DataSource = model.DataSource,
            CorrelationId = model.CorrelationId,
            LegacyId = model.LegacyId,
            SysCreatedAt = DateTime.UtcNow,
            SysUpdatedAt = DateTime.UtcNow,
            BloodGlucoseInput = model.BloodGlucoseInput,
            BloodGlucoseInputSource = model.BloodGlucoseInputSource,
            CarbInput = model.CarbInput,
            InsulinOnBoard = model.InsulinOnBoard,
            InsulinRecommendation = model.InsulinRecommendation,
            CarbRatio = model.CarbRatio,
            CalculationType = model.CalculationType?.ToString(),
            InsulinRecommendationForCarbs = model.InsulinRecommendationForCarbs,
            InsulinProgrammed = model.InsulinProgrammed,
            EnteredInsulin = model.EnteredInsulin,
            SplitNow = model.SplitNow,
            SplitExt = model.SplitExt,
            PreBolus = model.PreBolus,
            AdditionalPropertiesJson = model.AdditionalProperties is { Count: > 0 }
                ? JsonSerializer.Serialize(model.AdditionalProperties)
                : null,
        };
    }

    /// <summary>
    /// Convert database entity to domain model
    /// </summary>
    /// <param name="entity">The database entity to convert.</param>
    /// <returns>A new instance of BolusCalculation domain model.</returns>
    public static BolusCalculation ToDomainModel(BolusCalculationEntity entity)
    {
        return new BolusCalculation
        {
            Id = entity.Id,
            Timestamp = entity.Timestamp,
            UtcOffset = entity.UtcOffset,
            Device = entity.Device,
            App = entity.App,
            DataSource = entity.DataSource,
            CorrelationId = entity.CorrelationId,
            LegacyId = entity.LegacyId,
            CreatedAt = entity.SysCreatedAt,
            ModifiedAt = entity.SysUpdatedAt,
            BloodGlucoseInput = entity.BloodGlucoseInput,
            BloodGlucoseInputSource = entity.BloodGlucoseInputSource,
            CarbInput = entity.CarbInput,
            InsulinOnBoard = entity.InsulinOnBoard,
            InsulinRecommendation = entity.InsulinRecommendation,
            CarbRatio = entity.CarbRatio,
            CalculationType = Enum.TryParse<CalculationType>(entity.CalculationType, out var ct) ? ct : null,
            InsulinRecommendationForCarbs = entity.InsulinRecommendationForCarbs,
            InsulinProgrammed = entity.InsulinProgrammed,
            EnteredInsulin = entity.EnteredInsulin,
            SplitNow = entity.SplitNow,
            SplitExt = entity.SplitExt,
            PreBolus = entity.PreBolus,
            AdditionalProperties = !string.IsNullOrEmpty(entity.AdditionalPropertiesJson)
                ? JsonSerializer.Deserialize<Dictionary<string, object?>>(entity.AdditionalPropertiesJson)
                : null,
        };
    }

    /// <summary>
    /// Update existing entity with data from domain model
    /// </summary>
    /// <param name="entity">The database entity to update.</param>
    /// <param name="model">The domain model containing updated data.</param>
    public static void UpdateEntity(BolusCalculationEntity entity, BolusCalculation model)
    {
        entity.Timestamp = model.Timestamp;
        entity.UtcOffset = model.UtcOffset;
        entity.Device = model.Device;
        entity.App = model.App;
        entity.DataSource = model.DataSource;
        entity.CorrelationId = model.CorrelationId;
        entity.LegacyId = model.LegacyId;
        entity.BloodGlucoseInput = model.BloodGlucoseInput;
        entity.BloodGlucoseInputSource = model.BloodGlucoseInputSource;
        entity.CarbInput = model.CarbInput;
        entity.InsulinOnBoard = model.InsulinOnBoard;
        entity.InsulinRecommendation = model.InsulinRecommendation;
        entity.CarbRatio = model.CarbRatio;
        entity.CalculationType = model.CalculationType?.ToString();
        entity.InsulinRecommendationForCarbs = model.InsulinRecommendationForCarbs;
        entity.InsulinProgrammed = model.InsulinProgrammed;
        entity.EnteredInsulin = model.EnteredInsulin;
        entity.SplitNow = model.SplitNow;
        entity.SplitExt = model.SplitExt;
        entity.PreBolus = model.PreBolus;
        entity.AdditionalPropertiesJson = model.AdditionalProperties is { Count: > 0 }
            ? JsonSerializer.Serialize(model.AdditionalProperties)
            : null;
    }
}
