namespace Nocturne.Core.Models.V4;

/// <summary>
/// The origin a temp basal gets when its upload states none, and the
/// <see cref="TempBasalOriginEvidence"/> a stored origin rests on.
/// </summary>
/// <remarks>
/// No closed-loop uploader marks a temp basal as set by the loop or by hand: Trio drops Loop's flag for
/// temps, AAPS sends <c>type: NORMAL</c> for both, Medtronic history behind OpenAPS does not record who
/// set one, and Loop 2.x omits the flag. Their temps default to
/// <see cref="TempBasalOrigin.Algorithm"/>, and every default is left as
/// <see cref="TempBasalOriginEvidence.Uploader"/> for the APS snapshots to confirm or overturn.
/// </remarks>
public static class TempBasalOriginProvenance
{
    /// <summary>The field a projected treatment carries the evidence in, beside <see cref="OriginField"/>.</summary>
    public const string EvidenceField = "originEvidence";

    /// <summary>The field a projected treatment carries the stored origin in.</summary>
    public const string OriginField = "basalOrigin";

    private static readonly string[] AidUploaders = ["Trio", "iAPS", "freeaps-x"];

    private static readonly string[] AidUploaderSchemes = ["openaps://", "loop://"];

    public static bool IsAidUploader(string? enteredBy, string? app) =>
        string.Equals(app, "AAPS", StringComparison.OrdinalIgnoreCase)
        || enteredBy is not null
        && (AidUploaders.Any(u => string.Equals(enteredBy, u, StringComparison.OrdinalIgnoreCase))
            || AidUploaderSchemes.Any(s => enteredBy.StartsWith(s, StringComparison.OrdinalIgnoreCase)));

    public static TempBasalOrigin UploaderDefault(string? enteredBy, string? app) =>
        IsAidUploader(enteredBy, app) ? TempBasalOrigin.Algorithm : TempBasalOrigin.Manual;

    /// <summary>
    /// The evidence a stored origin rests on. A row stored before evidence was recorded holds a
    /// default exactly when it is Manual or Algorithm without an uploaded <c>automatic</c> flag, since
    /// every writer that states either keeps the flag (<see cref="TempBasalAutomaticFlag"/>).
    /// </summary>
    public static TempBasalOriginEvidence EvidenceOf(
        TempBasalOrigin origin, TempBasalOriginEvidence? stored, bool? automaticFlag) =>
        stored ?? (origin is TempBasalOrigin.Manual or TempBasalOrigin.Algorithm && automaticFlag is null
            ? TempBasalOriginEvidence.Uploader
            : TempBasalOriginEvidence.Reported);

    /// <inheritdoc cref="EvidenceOf(TempBasalOrigin, TempBasalOriginEvidence?, bool?)"/>
    public static TempBasalOriginEvidence EvidenceOf(TempBasal tempBasal) =>
        EvidenceOf(tempBasal.Origin, tempBasal.OriginEvidence, TempBasalAutomaticFlag.Of(tempBasal.AdditionalProperties));
}
