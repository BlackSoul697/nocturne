namespace Nocturne.Core.Contracts.Monitoring;

/// <summary>
/// Request to apply a consumable template, creating a tracker definition and alarm rules.
/// </summary>
/// <param name="ConsumableCatalogId">Identifier of the consumable catalog entry to scaffold from.</param>
/// <param name="LifespanHoursOverride">User-entered lifespan for non-hard-cutoff consumables.</param>
public record TemplateApplication(
    string ConsumableCatalogId,
    int? LifespanHoursOverride
);

/// <summary>
/// Result of applying a template, containing the created tracker definition and alarm rule IDs.
/// </summary>
/// <param name="TrackerDefinitionId">The created tracker definition identifier.</param>
/// <param name="AlertRuleIds">The created alarm rule identifiers.</param>
public record TemplateResult(
    Guid TrackerDefinitionId,
    IReadOnlyList<Guid> AlertRuleIds
);

/// <summary>
/// A template available for scaffolding based on the user's registered patient devices.
/// </summary>
/// <param name="ConsumableCatalogId">Consumable catalog entry identifier.</param>
/// <param name="Name">Human-readable name (e.g., "Sensor").</param>
/// <param name="Icon">Lucide icon name.</param>
/// <param name="DefaultLifespanHours">Default lifespan in hours, or null when user-variable.</param>
/// <param name="IsHardCutoff">Whether the device enforces a hard cutoff.</param>
/// <param name="DeviceName">Device name from the catalog (e.g., "Dexcom G7"), or null for universal consumables.</param>
public record AvailableTemplate(
    string ConsumableCatalogId,
    string Name,
    string Icon,
    int? DefaultLifespanHours,
    bool IsHardCutoff,
    string? DeviceName
);

/// <summary>
/// Creates tracker definitions and alarm rules from consumable catalog templates.
/// One-shot fire-and-forget: templates create concrete objects and stamp
/// <c>source_template</c> metadata on the generated alarm rules.
/// </summary>
public interface ITrackerTemplateService
{
    /// <summary>
    /// Returns available templates based on the user's registered patient devices.
    /// </summary>
    Task<IReadOnlyList<AvailableTemplate>> GetAvailableTemplatesAsync(CancellationToken ct = default);

    /// <summary>
    /// Applies a template: creates a tracker definition and 2 alarm rules.
    /// </summary>
    Task<TemplateResult> ApplyTemplateAsync(TemplateApplication request, CancellationToken ct = default);
}
