using System.ComponentModel.DataAnnotations;

namespace Nocturne.API.Services.Loopalyzer;

public sealed record LoopalyzerRequest
{
    /// <summary>Inclusive start date in patient timezone (YYYY-MM-DD).</summary>
    [Required] public required string From { get; init; }

    /// <summary>Inclusive end date in patient timezone (YYYY-MM-DD).</summary>
    [Required] public required string To { get; init; }
}

public sealed record LoopalyzerResponse(
    IReadOnlyList<LoopalyzerDay> Days,
    IReadOnlyList<LoopalyzerProfile> Profiles,
    string Timezone,
    double? MostRecentDia,
    double? MostRecentBgLow,
    double? MostRecentBgHigh
);

public sealed record LoopalyzerDay(
    string Date,
    double?[] Sgv,
    double[] ScheduledBasal,
    double?[] TempBasal,
    double?[] Iob,
    double?[] Cob,
    IReadOnlyList<LoopalyzerMeal> Meals,
    IReadOnlyList<LoopalyzerBolus> Boluses,
    IReadOnlyList<LoopalyzerSiteEvent> SiteChanges,
    IReadOnlyList<LoopalyzerSiteEvent> SensorChanges,
    IReadOnlyList<LoopalyzerPrediction> Predictions,
    IReadOnlyList<LoopalyzerApsBand> ApsBands,
    double Dia,
    bool HasApsData
);

public sealed record LoopalyzerMeal(int Minute, double Carbs, string EventType);

public sealed record LoopalyzerBolus(int Minute, double Units);

public sealed record LoopalyzerSiteEvent(int Minute, string? Note);

public sealed record LoopalyzerPrediction(int Minute, double[]? Iob, double[]? Zt, double[]? Cob, double[]? Uam);

public sealed record LoopalyzerApsBand(int StartMinute, int EndMinute, string Mode);

public sealed record LoopalyzerProfile(
    string Name,
    string ValidFrom,
    string? ValidTo,
    double Dia,
    IReadOnlyList<LoopalyzerScheduleEntry> Basal,
    IReadOnlyList<LoopalyzerScheduleEntry> Sensitivity,
    IReadOnlyList<LoopalyzerScheduleEntry> CarbRatio,
    double? BgLow,
    double? BgHigh
);

public sealed record LoopalyzerScheduleEntry(string Time, double Value);

public sealed record LoopalyzerAvailability(bool HasApsData, DateTimeOffset? LatestApsAt);
