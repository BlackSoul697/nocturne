namespace Nocturne.Core.Models.V4;

/// <summary>
/// A known consumable item in the <see cref="ConsumableCatalog"/>.
/// </summary>
public record ConsumableCatalogEntry
{
    /// <summary>Unique kebab-case identifier (e.g., "infusion-set").</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable display name.</summary>
    public required string Name { get; init; }

    /// <summary>Consumable type discriminator.</summary>
    public required ConsumableType ConsumableType { get; init; }

    /// <summary>Default lifespan in hours. Null when user-variable.</summary>
    public int? DefaultLifespanHours { get; init; }

    /// <summary>True when the device enforces a hard cutoff (e.g., pod auto-shutoff).</summary>
    public bool IsHardCutoff { get; init; }

    /// <summary>Device category this consumable applies to. Null for universal items.</summary>
    public DeviceCategory? ApplicableDeviceCategory { get; init; }

    /// <summary>Pump form factor filter. Null if applicable to all form factors.</summary>
    public PumpFormFactor? ApplicablePumpFormFactor { get; init; }

    /// <summary>Default tracker category when generating a tracker definition.</summary>
    public required TrackerCategory DefaultTrackerCategory { get; init; }

    /// <summary>Default Lucide icon name for the tracker.</summary>
    public required string DefaultIcon { get; init; }
}
