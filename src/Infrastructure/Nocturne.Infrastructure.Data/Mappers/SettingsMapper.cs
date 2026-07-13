using System.Text.Json;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Common;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Mappers;

/// <summary>
/// Mapper for converting between Settings domain models and SettingsEntity database entities
/// </summary>
public static class SettingsMapper
{
    /// <summary>
    /// Convert domain model to database entity
    /// </summary>
    public static SettingsEntity ToEntity(Settings settings)
    {
        return new SettingsEntity
        {
            Id = string.IsNullOrEmpty(settings.Id)
                ? Guid.CreateVersion7()
                : ParseIdToGuid(settings.Id),
            OriginalId = MongoIdUtils.IsValidMongoId(settings.Id) ? settings.Id : null,
            Key = settings.Key,
            Value = settings.Value != null ? JsonSerializer.Serialize(settings.Value) : null,
            CreatedAt = settings.CreatedAt,
            Mills = settings.Mills,
            UtcOffset = settings.UtcOffset,
            SrvCreated = settings.SrvCreated,
            SrvModified = settings.SrvModified,
            App = settings.App,
            Device = settings.Device,
            EnteredBy = settings.EnteredBy,
            Version = settings.Version,
            IsActive = settings.IsActive,
            Notes = settings.Notes,
            SysCreatedAt = DateTime.UtcNow,
            SysUpdatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Convert database entity to domain model
    /// </summary>
    public static Settings ToDomainModel(SettingsEntity entity)
    {
        object? value = null;
        if (!string.IsNullOrEmpty(entity.Value))
        {
            try
            {
                value = JsonSerializer.Deserialize<object>(entity.Value);
            }
            catch (JsonException)
            {
                // If JSON deserialization fails, treat as string value
                value = entity.Value;
            }
        }

        return new Settings
        {
            Id = entity.OriginalId ?? entity.Id.ToString(),
            Key = entity.Key,
            Value = value,
            CreatedAt = entity.CreatedAt,
            Mills = entity.Mills,
            UtcOffset = entity.UtcOffset,
            SrvCreated = entity.SrvCreated,
            SrvModified = entity.SrvModified,
            App = entity.App,
            Device = entity.Device,
            EnteredBy = entity.EnteredBy,
            Version = entity.Version,
            IsActive = entity.IsActive,
            Notes = entity.Notes,
        };
    }

    /// <summary>
    /// Update entity with values from domain model
    /// </summary>
    public static void UpdateEntity(SettingsEntity entity, Settings settings)
    {
        entity.Key = settings.Key;
        entity.Value = settings.Value != null ? JsonSerializer.Serialize(settings.Value) : null;
        entity.CreatedAt = settings.CreatedAt;
        entity.Mills = settings.Mills;
        entity.UtcOffset = settings.UtcOffset;
        entity.SrvCreated = settings.SrvCreated;
        entity.SrvModified = settings.SrvModified;
        entity.App = settings.App;
        entity.Device = settings.Device;
        entity.EnteredBy = settings.EnteredBy;
        entity.Version = settings.Version;
        entity.IsActive = settings.IsActive;
        entity.Notes = settings.Notes;
    }

    /// <summary>
    /// Convert string ID to GUID for consistent mapping
    /// </summary>
    private static Guid ParseIdToGuid(string id)
    {
        if (Guid.TryParse(id, out var guid))
        {
            return guid;
        }

        // For string IDs, create a deterministic GUID
        // This ensures consistent mapping between string ID and GUID
        var bytes = System.Text.Encoding.UTF8.GetBytes(id.PadRight(16, '0')[..16]);
        return new Guid(bytes);
    }
}
