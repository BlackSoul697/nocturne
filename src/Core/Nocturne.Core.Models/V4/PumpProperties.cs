namespace Nocturne.Core.Models.V4;

/// <summary>
/// Pump-specific properties for a <see cref="DeviceCatalogEntry"/>.
/// </summary>
public record PumpProperties
{
    /// <summary>
    /// Physical form factor (patch or tubed).
    /// </summary>
    public required PumpFormFactor FormFactor { get; init; }

    /// <summary>
    /// Reservoir capacity in insulin units (e.g., 200 for Omnipod 5, 300 for t:slim X2).
    /// </summary>
    public int? ReservoirCapacityUnits { get; init; }
}
