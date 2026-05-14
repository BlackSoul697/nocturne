namespace Nocturne.Core.Models.V4;

/// <summary>
/// Physical form factor of an insulin pump.
/// </summary>
public enum PumpFormFactor
{
    /// <summary>Patch pump (tubeless, worn on body). E.g., Omnipod.</summary>
    Patch,

    /// <summary>Tubed pump (connected via infusion set tubing). E.g., t:slim, MiniMed.</summary>
    Tubed
}
