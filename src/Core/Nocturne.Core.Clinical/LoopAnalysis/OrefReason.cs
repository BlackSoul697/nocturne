using System.Globalization;
using System.Text.RegularExpressions;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Clinical.LoopAnalysis;

/// <summary>What an oref suggestion's <c>reason</c> says about the limits that cut it.</summary>
/// <param name="CappedAtMaxSafeBasal">
/// The temp asked for was cut to <c>maxSafeBasal</c>, the lowest of <c>max_basal</c> and the two
/// safety multiples of the schedule. determine-basal says so ("adj. req. rate: X to maxSafeBasal:
/// Y") whenever it cuts a rate, so a reason without it means the temp was not cut.
/// </param>
/// <param name="MaxSafeBasal">U/h, where the reason gives it.</param>
/// <param name="AtMaxIob">
/// IOB was at <c>max_iob</c>: the insulin asked for was cut to it ("max_iob N") or IOB already
/// exceeded it ("IOB X &gt; max_iob N").
/// </param>
/// <param name="MaxIob">U, where the reason gives it.</param>
public readonly record struct OrefReason(bool CappedAtMaxSafeBasal, double? MaxSafeBasal, bool AtMaxIob, double? MaxIob)
{
    private static readonly Regex MaxSafeBasalPattern = new(@"maxSafeBasal:\s*(\d+(?:\.\d+)?)", RegexOptions.CultureInvariant);
    private static readonly Regex MaxIobPattern = new(@"max_iob\s+(\d+(?:\.\d+)?)", RegexOptions.CultureInvariant);

    /// <param name="limitReason">See <see cref="ApsLoopCycle.LimitReason"/>.</param>
    public static OrefReason Parse(string? limitReason)
    {
        if (string.IsNullOrEmpty(limitReason))
            return default;

        var maxSafeBasal = MaxSafeBasalPattern.Match(limitReason);
        var maxIob = MaxIobPattern.Match(limitReason);
        return new OrefReason(
            maxSafeBasal.Success,
            maxSafeBasal.Success ? Number(maxSafeBasal) : null,
            limitReason.Contains("max_iob", StringComparison.Ordinal),
            maxIob.Success ? Number(maxIob) : null);
    }

    private static double Number(Match match) => double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
}
