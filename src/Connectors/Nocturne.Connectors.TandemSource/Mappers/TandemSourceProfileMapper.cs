using Nocturne.Connectors.TandemSource.Models;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.TandemSource.Mappers;

/// <summary>
/// Maps Tandem Source pump profile settings to Nocturne Profile records.
/// Profile data comes from pump event metadata (lastUpload.settings), not from events.
/// </summary>
public static class TandemSourceProfileMapper
{
    public static Profile? Map(PumpSettings? settings, string timezone)
    {
        if (settings?.Profiles == null) return null;

        var profiles = settings.Profiles;
        var store = new Dictionary<string, ProfileData>();

        PumpProfile? activeProfile = null;

        foreach (var pumpProfile in profiles.Profile)
        {
            if (pumpProfile.Idp == profiles.ActiveIdp)
                activeProfile = pumpProfile;

            store[pumpProfile.Name] = MapProfileData(pumpProfile, timezone);
        }

        if (store.Count == 0) return null;

        return new Profile
        {
            DefaultProfile = activeProfile?.Name ?? profiles.Profile.FirstOrDefault()?.Name ?? "Default",
            Store = store,
            EnteredBy = DataSources.TandemSourceConnector,
            StartDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private static ProfileData MapProfileData(PumpProfile pumpProfile, string timezone)
    {
        var basalSegments = new List<TimeValue>();
        var isfSegments = new List<TimeValue>();
        var carbRatioSegments = new List<TimeValue>();
        var targetLowSegments = new List<TimeValue>();
        var targetHighSegments = new List<TimeValue>();

        foreach (var seg in pumpProfile.TDependentSegs.OrderBy(s => s.StartTime))
        {
            var timeStr = MinutesToTimeString(seg.StartTime);
            var timeAsSeconds = seg.StartTime * 60;

            basalSegments.Add(new TimeValue
            {
                Time = timeStr,
                Value = Math.Round(seg.BasalRate / 1000.0, 3),
                TimeAsSeconds = timeAsSeconds
            });

            isfSegments.Add(new TimeValue
            {
                Time = timeStr,
                Value = seg.Isf,
                TimeAsSeconds = timeAsSeconds
            });

            carbRatioSegments.Add(new TimeValue
            {
                Time = timeStr,
                Value = seg.CarbRatio,
                TimeAsSeconds = timeAsSeconds
            });

            targetLowSegments.Add(new TimeValue
            {
                Time = timeStr,
                Value = seg.TargetBg,
                TimeAsSeconds = timeAsSeconds
            });

            targetHighSegments.Add(new TimeValue
            {
                Time = timeStr,
                Value = seg.TargetBg,
                TimeAsSeconds = timeAsSeconds
            });
        }

        return new ProfileData
        {
            Dia = pumpProfile.InsulinDuration / 60.0,
            Basal = basalSegments,
            Sens = isfSegments,
            CarbRatio = carbRatioSegments,
            TargetLow = targetLowSegments,
            TargetHigh = targetHighSegments,
            Units = "mg/dl",
            Timezone = timezone
        };
    }

    private static string MinutesToTimeString(int minutes)
    {
        var hours = minutes / 60;
        var mins = minutes % 60;
        return $"{hours:D2}:{mins:D2}";
    }
}
