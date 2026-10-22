using Nocturne.Core.Constants;

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
    /// default only when it is a Manual or Algorithm temp without an uploaded <c>automatic</c> flag
    /// that the decomposer wrote for a closed-loop uploader; the flag was kept only from late 2026, so
    /// a connector's older rows carry none and their origin is still the pump's statement.
    /// </summary>
    /// <param name="enteredBy">The uploader, which the decomposer stores as the record's <c>app</c>.</param>
    /// <param name="dataSource">The record's data source, which names the connector that wrote it, if one did.</param>
    public static TempBasalOriginEvidence EvidenceOf(
        TempBasalOrigin origin,
        TempBasalOriginEvidence? stored,
        bool? automaticFlag,
        string? enteredBy,
        string? dataSource) =>
        stored ?? (origin is TempBasalOrigin.Manual or TempBasalOrigin.Algorithm
                   && automaticFlag is null
                   && IsAidUploader(enteredBy, app: null)
                   && !ConnectorSources.Contains(dataSource ?? string.Empty)
            ? TempBasalOriginEvidence.Uploader
            : TempBasalOriginEvidence.Reported);

    /// <summary>
    /// How near two rates must be to count as the same rate: just under the 0.05 U/h step of the
    /// coarsest pumps, which deliver a proposed rate rounded to their step, so a rate one whole step
    /// away is a different rate.
    /// </summary>
    public const double RateTolerance = 0.0495;

    /// <summary>
    /// The origin a write leaves on a stored temp basal. An incoming statement
    /// (<see cref="TempBasalOriginEvidence.Reported"/>) wins. An incoming default
    /// (<see cref="TempBasalOriginEvidence.Uploader"/> or none) leaves a stored statement as it is, and
    /// a stored snapshot judgement as it is while the temp's start and rate are the ones it was judged
    /// on, with its snapshot link, since a re-upload carries no evidence of its own. A temp that moved
    /// or changed rate takes the incoming default, so it is judged again. Anything else is taken as sent.
    /// </summary>
    public static TempBasalOriginState OnUpdate(TempBasalOriginState stored, TempBasalOriginState incoming) =>
        incoming.Evidence is null or TempBasalOriginEvidence.Uploader && KeepsStored(stored, incoming)
            ? stored
            : incoming;

    private static bool KeepsStored(TempBasalOriginState stored, TempBasalOriginState incoming) =>
        stored.Evidence is TempBasalOriginEvidence.Reported
        || stored.Evidence is TempBasalOriginEvidence.Snapshot && SameTemp(stored, incoming);

    private static bool SameTemp(TempBasalOriginState stored, TempBasalOriginState incoming) =>
        stored.StartMills == incoming.StartMills
        && (stored.Rate is { } storedRate && incoming.Rate is { } incomingRate
            ? Math.Abs(storedRate - incomingRate) < SameRateEpsilon
            : stored.Rate is null && incoming.Rate is null);

    /// <summary>
    /// Floating-point noise only, not <see cref="RateTolerance"/>: two rates within a pump step of each
    /// other can fall on opposite sides of a proposal's tolerance, so any real change is judged again.
    /// </summary>
    private const double SameRateEpsilon = 1e-6;

    /// <inheritdoc cref="EvidenceOf(TempBasalOrigin, TempBasalOriginEvidence?, bool?, string?, string?)"/>
    public static TempBasalOriginEvidence EvidenceOf(TempBasal tempBasal) =>
        EvidenceOf(
            tempBasal.Origin,
            tempBasal.OriginEvidence,
            TempBasalAutomaticFlag.Of(tempBasal.AdditionalProperties),
            tempBasal.App,
            tempBasal.DataSource);

    /// <summary>
    /// The connectors that wrote temp basals themselves rather than through the decomposer: a closed
    /// set for rows stored before evidence was recorded. A new connector need not be added, since
    /// every writer now records evidence and no connector sets <see cref="TempBasal.App"/>.
    /// </summary>
    private static readonly HashSet<string> ConnectorSources = new(StringComparer.OrdinalIgnoreCase)
    {
        DataSources.CareLinkConnector,
        DataSources.MiniMedConnector,
        DataSources.GlookoConnector,
        DataSources.MyLifeConnector,
        DataSources.TConnectSyncConnector,
        DataSources.TwiistConnector,
    };
}

/// <summary>
/// The three fields that together state a temp basal's origin, with the start and rate a snapshot
/// judgement of it rests on.
/// </summary>
public readonly record struct TempBasalOriginState(
    TempBasalOrigin Origin,
    TempBasalOriginEvidence? Evidence,
    Guid? ApsSnapshotId,
    long StartMills,
    double? Rate);
