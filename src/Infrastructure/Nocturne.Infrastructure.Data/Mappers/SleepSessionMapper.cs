using System.Text.Json;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Mappers;

/// <summary>
/// Mapper for converting between SleepSession domain models and SleepSessionEntity database entities.
/// </summary>
public static class SleepSessionMapper
{
    /// <summary>
    /// Convert domain model to database entity.
    /// </summary>
    public static SleepSessionEntity ToEntity(SleepSession session, Guid tenantId)
    {
        var entityId = string.IsNullOrEmpty(session.Id)
            ? Guid.CreateVersion7()
            : ParseIdToGuid(session.Id);

        var entity = new SleepSessionEntity
        {
            Id = entityId,
            TenantId = tenantId,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Timezone = session.Timezone,
            Type = session.Type.ToString(),
            DetectionMethod = session.DetectionMethod.ToString(),
            IsMainSleep = session.IsMainSleep,
            DurationMs = session.DurationMs,
            TotalSleepMs = session.TotalSleepMs,
            TotalAwakeMs = session.TotalAwakeMs,
            DeepSleepMs = session.DeepSleepMs,
            LightSleepMs = session.LightSleepMs,
            RemSleepMs = session.RemSleepMs,
            SleepLatencyMs = session.SleepLatencyMs,
            Efficiency = session.Efficiency,
            RestlessPeriods = session.RestlessPeriods,
            SleepScore = session.SleepScore,
            AvgHeartRate = session.AvgHeartRate,
            MinHeartRate = session.MinHeartRate,
            AvgHrv = session.AvgHrv,
            AvgBreathRate = session.AvgBreathRate,
            AvgSpo2 = session.AvgSpo2,
            Source = session.Source.ToString(),
            SourceDevice = session.SourceDevice,
            SourceApp = session.SourceApp,
            OriginalId = session.OriginalId,
            MetadataJson = session.Metadata != null
                ? JsonSerializer.Serialize(session.Metadata)
                : null,
            CreatedAt = session.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        if (session.Stages is { Count: > 0 })
        {
            entity.Stages = session.Stages.Select(s => new SleepStageEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                SleepSessionId = entityId,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Stage = s.Stage.ToString(),
                Ordinal = s.Ordinal,
            }).ToList();
        }

        if (session.BiometricSamples is { Count: > 0 })
        {
            entity.BiometricSamples = session.BiometricSamples.Select(b => new SleepBiometricSampleEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                SleepSessionId = entityId,
                Timestamp = b.Timestamp,
                HeartRate = b.HeartRate,
                Hrv = b.Hrv,
                Spo2 = b.Spo2,
                RespirationRate = b.RespirationRate,
                Movement = b.Movement,
            }).ToList();
        }

        return entity;
    }

    /// <summary>
    /// Convert database entity to domain model.
    /// </summary>
    public static SleepSession ToDomainModel(SleepSessionEntity entity, bool includeChildren = false)
    {
        var session = new SleepSession
        {
            Id = entity.OriginalId ?? entity.Id.ToString(),
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Timezone = entity.Timezone,
            Type = Enum.TryParse<SleepSessionType>(entity.Type, out var type)
                ? type
                : SleepSessionType.Unknown,
            DetectionMethod = Enum.TryParse<SleepDetectionMethod>(entity.DetectionMethod, out var method)
                ? method
                : SleepDetectionMethod.Unknown,
            IsMainSleep = entity.IsMainSleep,
            DurationMs = entity.DurationMs,
            TotalSleepMs = entity.TotalSleepMs,
            TotalAwakeMs = entity.TotalAwakeMs,
            DeepSleepMs = entity.DeepSleepMs,
            LightSleepMs = entity.LightSleepMs,
            RemSleepMs = entity.RemSleepMs,
            SleepLatencyMs = entity.SleepLatencyMs,
            Efficiency = entity.Efficiency,
            RestlessPeriods = entity.RestlessPeriods,
            SleepScore = entity.SleepScore,
            AvgHeartRate = entity.AvgHeartRate,
            MinHeartRate = entity.MinHeartRate,
            AvgHrv = entity.AvgHrv,
            AvgBreathRate = entity.AvgBreathRate,
            AvgSpo2 = entity.AvgSpo2,
            Source = Enum.TryParse<SleepSource>(entity.Source, out var source)
                ? source
                : SleepSource.Manual,
            SourceDevice = entity.SourceDevice,
            SourceApp = entity.SourceApp,
            OriginalId = entity.OriginalId,
            Metadata = DeserializeMetadata(entity.MetadataJson),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };

        if (includeChildren)
        {
            session.Stages = entity.Stages
                .OrderBy(s => s.Ordinal)
                .Select(s => new SleepStageInterval
                {
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Stage = Enum.TryParse<SleepStageType>(s.Stage, out var stage)
                        ? stage
                        : SleepStageType.Unmeasurable,
                    Ordinal = s.Ordinal,
                })
                .ToList();

            session.BiometricSamples = entity.BiometricSamples
                .OrderBy(b => b.Timestamp)
                .Select(b => new SleepBiometricSample
                {
                    Timestamp = b.Timestamp,
                    HeartRate = b.HeartRate,
                    Hrv = b.Hrv,
                    Spo2 = b.Spo2,
                    RespirationRate = b.RespirationRate,
                    Movement = b.Movement,
                })
                .ToList();
        }

        return session;
    }

    /// <summary>
    /// Safely deserialize metadata JSON to a dictionary.
    /// </summary>
    private static Dictionary<string, object>? DeserializeMetadata(string? json)
    {
        if (string.IsNullOrEmpty(json) || json == "null")
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse string ID to GUID, or generate new GUID if invalid.
    /// </summary>
    private static Guid ParseIdToGuid(string id)
    {
        if (string.IsNullOrEmpty(id))
            return Guid.CreateVersion7();

        if (Guid.TryParse(id, out var guidId))
            return guidId;

        // Hash the ID to get a deterministic GUID
        try
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hashBytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id));
            var guidBytes = new byte[16];
            Array.Copy(hashBytes, guidBytes, 16);
            return new Guid(guidBytes);
        }
        catch
        {
            return Guid.CreateVersion7();
        }
    }
}
