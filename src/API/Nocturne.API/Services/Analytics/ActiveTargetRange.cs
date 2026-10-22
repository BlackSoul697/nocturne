using Nocturne.Core.Contracts.Profiles.Resolvers;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.Analytics;

internal static class ActiveTargetRange
{
    /// <summary>
    /// Returns the target range schedule active for the tenant's active profile right now, or null
    /// when none is configured. Shared selection policy for report statistics and AID metrics, kept
    /// in step with the alert engine and <c>TargetRangeResolver</c> (active profile, newest schedule
    /// at the point in time) so reports evaluate the same range alerts fire against — rather than the
    /// globally-newest record, which could belong to a different, inactive profile.
    /// </summary>
    public static async Task<TargetRangeSchedule?> ScheduleAsync(
        IActiveProfileResolver activeProfileResolver,
        ITargetRangeScheduleRepository targetRangeScheduleRepository,
        CancellationToken ct)
    {
        var nowMills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var profileName = await activeProfileResolver.GetProfileNameAsync(nowMills, ct);
        // Before the store's first version, its newest, as ProfileVersionCache.ActiveAt reads it.
        return await targetRangeScheduleRepository.GetActiveAtAsync(profileName, DateTime.UtcNow, ct)
            ?? (await targetRangeScheduleRepository.GetByProfileNameAsync(profileName, ct)).MaxBy(s => s.Timestamp);
    }
}
