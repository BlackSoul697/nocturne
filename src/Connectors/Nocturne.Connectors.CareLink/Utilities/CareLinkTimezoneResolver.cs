using Nocturne.Core.Models;

namespace Nocturne.Connectors.CareLink.Utilities;

/// <summary>
/// Resolves CareLink's <c>clientTimeZoneName</c> to an IANA zone id suitable for seeding the tenant
/// timezone timeline.
/// </summary>
/// <remarks>
/// CareLink reports a Windows DISPLAY name (e.g. "Australian Western Standard Time"), which is neither
/// a valid Windows id ("W. Australia Standard Time") nor an IANA zone, so the framework converters
/// can't resolve it. Resolution order: try the framework (handles genuine IANA/Windows ids) → fall
/// back to a curated display-name map → otherwise return null so the caller skips seeding. Never
/// returns "UTC" or a fixed offset: an unresolved zone is skipped, not silently defaulted to UTC.
/// </remarks>
public static class CareLinkTimezoneResolver
{
    private static readonly Dictionary<string, string> DisplayNameToIana = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Australian Western Standard Time"] = "Australia/Perth",
        ["Australian Central Standard Time"] = "Australia/Adelaide",
        ["Australian Central Western Standard Time"] = "Australia/Eucla",
        ["Australian Eastern Standard Time"] = "Australia/Sydney",
    };

    /// <summary>
    /// Returns the IANA zone id for the given CareLink <c>clientTimeZoneName</c>, or null when the
    /// name is blank, unresolved, or resolves to UTC.
    /// </summary>
    public static string? ResolveIana(string? clientTimeZoneName)
    {
        if (string.IsNullOrWhiteSpace(clientTimeZoneName))
            return null;

        var name = clientTimeZoneName.Trim();

        // Framework first: resolves real IANA or Windows ids directly.
        var tz = TimeZoneHelper.GetTimeZoneInfoFromId(name);
        if (tz.Id != TimeZoneInfo.Utc.Id)
            return ToIana(tz.Id);

        // The framework returns UTC on failure; fall back to the curated display-name map.
        if (DisplayNameToIana.TryGetValue(name, out var mapped))
            return mapped;

        return null;
    }

    private static string ToIana(string timeZoneId) =>
        TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out var iana) ? iana : timeZoneId;
}
