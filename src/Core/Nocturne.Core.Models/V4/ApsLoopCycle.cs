namespace Nocturne.Core.Models.V4;

/// <summary>
/// One loop cycle of an <see cref="ApsSnapshot"/>: what the algorithm did and reported, without the
/// prediction curves or the stored commands.
/// </summary>
/// <remarks>
/// The oref fields are read out of the stored suggestion (<see cref="ApsSnapshot.SuggestedJson"/>)
/// by the database, so the suggestion itself never leaves it. A limit is null unless the
/// suggestion carries its key with a number; oref's own reports do not carry them, so in practice
/// only a writer that stores them through the V4 API supplies them.
/// </remarks>
public sealed class ApsLoopCycle
{
    public DateTime Timestamp { get; init; }

    public long Mills => new DateTimeOffset(Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();

    public string? Device { get; init; }

    public AidAlgorithm AidAlgorithm { get; init; }

    /// <inheritdoc cref="ApsSnapshot.Enacted"/>
    public bool Enacted { get; init; }

    /// <summary>U/h.</summary>
    public double? EnactedRate { get; init; }

    /// <summary>Minutes.</summary>
    public int? EnactedDuration { get; init; }

    /// <summary>U.</summary>
    public double? EnactedBolusVolume { get; init; }

    /// <summary>U.</summary>
    public double? Iob { get; init; }

    /// <inheritdoc cref="ApsSnapshot.SensitivityRatio"/>
    public double? SensitivityRatio { get; init; }

    /// <summary>The cycle stored an oref suggestion.</summary>
    public bool HasSuggestion { get; init; }

    /// <summary>
    /// The suggestion carries a <c>reason</c>, so <see cref="LimitReason"/> being null says no limit
    /// cut it.
    /// </summary>
    public bool HasReason { get; init; }

    /// <summary>
    /// The suggestion asked for a temp basal (it carries a <c>duration</c>) or a microbolus (it
    /// carries <c>units</c>); null without a suggestion.
    /// </summary>
    public bool? SuggestedChange { get; init; }

    /// <summary>
    /// The suggestion's <c>reason</c>, only where it names <c>maxSafeBasal</c> or <c>max_iob</c>:
    /// oref says so whenever either limit cut what it asked for.
    /// </summary>
    public string? LimitReason { get; init; }

    /// <summary>oref <c>max_iob</c>, U.</summary>
    public double? MaxIob { get; init; }

    /// <summary>oref <c>max_basal</c>, U/h.</summary>
    public double? MaxBasal { get; init; }

    /// <summary>oref <c>max_daily_safety_multiplier</c>: the cap's multiple of the highest scheduled rate of the day.</summary>
    public double? MaxDailySafetyMultiplier { get; init; }

    /// <summary>oref <c>current_basal_safety_multiplier</c>: the cap's multiple of the scheduled rate in force.</summary>
    public double? CurrentBasalSafetyMultiplier { get; init; }
}
