using Microsoft.EntityFrameworkCore;
using Nocturne.Infrastructure.Data.Abstractions;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Alerts;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Repositories;

/// <summary>
/// PostgreSQL repository for Tracker operations (definitions, instances, presets).
/// All queries are tenant-scoped via RLS on the underlying DbContext.
/// </summary>
public class TrackerRepository : ITrackerRepository
{
    private readonly NocturneDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackerRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public TrackerRepository(NocturneDbContext context)
    {
        _context = context;
    }

    #region Definitions

    /// <inheritdoc />
    public virtual async Task<List<TrackerDefinitionEntity>> GetDefinitionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TrackerDefinitions.AsNoTracking()
            .Include(d => d.NotificationThresholds)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<List<TrackerDefinitionEntity>> GetDefinitionsByCategoryAsync(
        TrackerCategory category,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TrackerDefinitions.AsNoTracking()
            .Include(d => d.NotificationThresholds)
            .Where(d => d.Category == category)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TrackerDefinitionEntity[]> GetFavoriteDefinitionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TrackerDefinitions.AsNoTracking()
            .Where(d => d.IsFavorite)
            .OrderBy(d => d.Name)
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TrackerDefinitionEntity?> GetDefinitionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TrackerDefinitions.AsNoTracking()
            .Include(d => d.NotificationThresholds)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TrackerDefinitionEntity> CreateDefinitionAsync(
        TrackerDefinitionEntity definition,
        CancellationToken cancellationToken = default
    )
    {
        definition.Id = Guid.CreateVersion7();
        definition.CreatedAt = DateTime.UtcNow;

        _context.TrackerDefinitions.Add(definition);
        await _context.SaveChangesAsync(cancellationToken);

        return definition;
    }

    /// <inheritdoc />
    public virtual async Task<TrackerDefinitionEntity?> UpdateDefinitionAsync(
        Guid id,
        TrackerDefinitionEntity updated,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await _context.TrackerDefinitions.FirstOrDefaultAsync(
            d => d.Id == id,
            cancellationToken
        );

        if (existing == null)
            return null;

        existing.Name = updated.Name;
        existing.Description = updated.Description;
        existing.Category = updated.Category;
        existing.Icon = updated.Icon;
        existing.TriggerEventTypes = updated.TriggerEventTypes;
        existing.TriggerNotesContains = updated.TriggerNotesContains;
        existing.LifespanHours = updated.LifespanHours;
        existing.IsFavorite = updated.IsFavorite;
        existing.DashboardVisibility = updated.DashboardVisibility;
        existing.Visibility = updated.Visibility;
        existing.StartEventType = updated.StartEventType;
        existing.CompletionEventType = updated.CompletionEventType;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Reload thresholds to ensure we return the complete object
        await _context.Entry(existing).Collection(d => d.NotificationThresholds).LoadAsync(cancellationToken);

        return existing;
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteDefinitionAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var definition = await _context.TrackerDefinitions.FirstOrDefaultAsync(
            d => d.Id == id,
            cancellationToken
        );

        if (definition == null)
            return false;

        _context.TrackerDefinitions.Remove(definition);
        var result = await _context.SaveChangesAsync(cancellationToken);
        return result > 0;
    }

    /// <inheritdoc />
    public virtual async Task UpdateNotificationThresholdsAsync(
        Guid definitionId,
        List<TrackerNotificationThresholdEntity> thresholds,
        CancellationToken cancellationToken = default
    )
    {
        // Remove existing thresholds
        var existing = await _context
            .TrackerNotificationThresholds.Where(t => t.TrackerDefinitionId == definitionId)
            .ToListAsync(cancellationToken);

        _context.TrackerNotificationThresholds.RemoveRange(existing);

        // Add new thresholds
        foreach (var threshold in thresholds)
        {
            threshold.Id = Guid.CreateVersion7();
            threshold.TrackerDefinitionId = definitionId;
            _context.TrackerNotificationThresholds.Add(threshold);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    #endregion

    #region Instances

    /// <inheritdoc />
    public virtual async Task<TrackerInstanceEntity[]> GetActiveInstancesAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TrackerInstances.AsNoTracking()
            .Include(i => i.Definition)
            .Where(i => i.CompletedAt == null)
            .OrderByDescending(i => i.StartedAt)
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<List<TrackerInstanceEntity>> GetActiveInstancesForDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TrackerInstances.AsNoTracking()
            .Include(i => i.Definition)
            .Where(i => i.DefinitionId == definitionId && i.CompletedAt == null)
            .OrderByDescending(i => i.StartedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TrackerInstanceEntity[]> GetCompletedInstancesAsync(
        int limit = 100,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TrackerInstances.AsNoTracking()
            .Include(i => i.Definition)
            .Where(i => i.CompletedAt != null)
            .OrderByDescending(i => i.CompletedAt)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TrackerInstanceEntity[]> GetUpcomingInstancesAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default
    )
    {
        // Get active instances with lifespan defined
        var instances = await _context
            .TrackerInstances.AsNoTracking()
            .Include(i => i.Definition)
            .Where(i => i.CompletedAt == null && i.Definition.LifespanHours != null)
            .ToArrayAsync(cancellationToken);

        // Filter by expected end date (calculated in memory)
        return instances
            .Where(i =>
            {
                var expectedEnd = i.StartedAt.AddHours(i.Definition.LifespanHours!.Value);
                return expectedEnd >= from && expectedEnd <= to;
            })
            .ToArray();
    }

    /// <inheritdoc />
    public virtual async Task<TrackerInstanceEntity?> GetInstanceByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TrackerInstances.AsNoTracking()
            .Include(i => i.Definition)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TrackerInstanceEntity> StartInstanceAsync(
        Guid definitionId,
        string? startNotes = null,
        string? startTreatmentId = null,
        DateTime? startedAt = null,
        DateTime? scheduledAt = null,
        CancellationToken cancellationToken = default
    )
    {
        var instance = new TrackerInstanceEntity
        {
            Id = Guid.CreateVersion7(),
            DefinitionId = definitionId,
            StartedAt = startedAt ?? DateTime.UtcNow,
            StartNotes = startNotes,
            StartTreatmentId = startTreatmentId,
            ScheduledAt = scheduledAt,
        };

        _context.TrackerInstances.Add(instance);
        await _context.SaveChangesAsync(cancellationToken);

        // Load the definition for the returned entity
        await _context.Entry(instance).Reference(i => i.Definition).LoadAsync(cancellationToken);

        return instance;
    }

    /// <inheritdoc />
    public virtual async Task<TrackerInstanceEntity?> CompleteInstanceAsync(
        Guid instanceId,
        CompletionReason reason,
        string? completionNotes = null,
        string? completeTreatmentId = null,
        DateTime? completedAt = null,
        CancellationToken cancellationToken = default
    )
    {
        var instance = await _context
            .TrackerInstances.Include(i => i.Definition)
            .FirstOrDefaultAsync(i => i.Id == instanceId, cancellationToken);

        if (instance == null)
            return null;

        instance.CompletedAt = completedAt ?? DateTime.UtcNow;
        instance.CompletionReason = reason;
        instance.CompletionNotes = completionNotes;
        instance.CompleteTreatmentId = completeTreatmentId;

        await _context.SaveChangesAsync(cancellationToken);
        return instance;
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteInstanceAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var instance = await _context.TrackerInstances.FirstOrDefaultAsync(
            i => i.Id == id,
            cancellationToken
        );

        if (instance == null)
            return false;

        _context.TrackerInstances.Remove(instance);
        var result = await _context.SaveChangesAsync(cancellationToken);
        return result > 0;
    }

    #endregion

    #region Presets

    /// <inheritdoc />
    public virtual async Task<TrackerPresetEntity[]> GetPresetsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TrackerPresets.AsNoTracking()
            .Include(p => p.Definition)
            .OrderBy(p => p.Name)
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TrackerPresetEntity?> GetPresetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .TrackerPresets.AsNoTracking()
            .Include(p => p.Definition)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TrackerPresetEntity> CreatePresetAsync(
        TrackerPresetEntity preset,
        CancellationToken cancellationToken = default
    )
    {
        preset.Id = Guid.CreateVersion7();
        preset.CreatedAt = DateTime.UtcNow;

        _context.TrackerPresets.Add(preset);
        await _context.SaveChangesAsync(cancellationToken);

        return preset;
    }

    /// <inheritdoc />
    public virtual async Task<TrackerInstanceEntity?> ApplyPresetAsync(
        Guid presetId,
        string? overrideNotes = null,
        CancellationToken cancellationToken = default
    )
    {
        var preset = await _context
            .TrackerPresets.Include(p => p.Definition)
            .FirstOrDefaultAsync(p => p.Id == presetId, cancellationToken);

        if (preset == null)
            return null;

        var notes = overrideNotes ?? preset.DefaultStartNotes;
        return await StartInstanceAsync(
            preset.DefinitionId,
            notes,
            cancellationToken: cancellationToken
        );
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeletePresetAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var preset = await _context.TrackerPresets.FirstOrDefaultAsync(
            p => p.Id == id,
            cancellationToken
        );

        if (preset == null)
            return false;

        _context.TrackerPresets.Remove(preset);
        var result = await _context.SaveChangesAsync(cancellationToken);
        return result > 0;
    }

    #endregion

    #region Snapshots

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyList<TrackerSnapshot>> GetTrackerSnapshotsAsync(CancellationToken ct = default)
    {
        var snapshots = await _context.TrackerDefinitions
            .AsNoTracking()
            .GroupJoin(
                _context.TrackerInstances.AsNoTracking().Where(i => i.CompletedAt == null),
                d => d.Id,
                i => i.DefinitionId,
                (d, instances) => new { Definition = d, Instances = instances })
            .SelectMany(
                x => x.Instances.DefaultIfEmpty(),
                (x, instance) => new TrackerSnapshot(
                    x.Definition.Id,
                    instance != null ? instance.Id : null,
                    x.Definition.Category,
                    x.Definition.Mode,
                    instance != null ? new DateTimeOffset(instance.StartedAt, TimeSpan.Zero) : null,
                    instance != null && instance.ScheduledAt != null
                        ? new DateTimeOffset(instance.ScheduledAt.Value, TimeSpan.Zero)
                        : null,
                    x.Definition.LifespanHours,
                    instance != null))
            .ToListAsync(ct);

        return snapshots;
    }

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyList<TrackerSnapshot>> GetTrackerSnapshotsAsOfAsync(
        DateTimeOffset asOf, CancellationToken ct = default)
    {
        var asOfUtc = asOf.UtcDateTime;

        var snapshots = await _context.TrackerDefinitions
            .AsNoTracking()
            .GroupJoin(
                _context.TrackerInstances.AsNoTracking()
                    .Where(i => i.StartedAt <= asOfUtc && (i.CompletedAt == null || i.CompletedAt > asOfUtc)),
                d => d.Id,
                i => i.DefinitionId,
                (d, instances) => new { Definition = d, Instances = instances })
            .SelectMany(
                x => x.Instances.DefaultIfEmpty(),
                (x, instance) => new TrackerSnapshot(
                    x.Definition.Id,
                    instance != null ? instance.Id : null,
                    x.Definition.Category,
                    x.Definition.Mode,
                    instance != null ? new DateTimeOffset(instance.StartedAt, TimeSpan.Zero) : null,
                    instance != null && instance.ScheduledAt != null
                        ? new DateTimeOffset(instance.ScheduledAt.Value, TimeSpan.Zero)
                        : null,
                    x.Definition.LifespanHours,
                    instance != null))
            .ToListAsync(ct);

        return snapshots;
    }

    #endregion
}
