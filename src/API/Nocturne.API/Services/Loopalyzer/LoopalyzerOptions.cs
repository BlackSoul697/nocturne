namespace Nocturne.API.Services.Loopalyzer;

public sealed class LoopalyzerOptions
{
    public const string SectionName = "Loopalyzer";

    /// <summary>Maximum range in days. Server enforces; UI clamps.</summary>
    public int MaxRangeDays { get; init; } = 14;

    /// <summary>Cache TTL for past days.</summary>
    public TimeSpan PastDayCacheTtl { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Cache TTL for today (still being written).</summary>
    public TimeSpan TodayCacheTtl { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Largest gap (5-min ticks) bridged when interpolating rising series. Legacy: 6.</summary>
    public int RisingInterpolationGap { get; init; } = 6;

    /// <summary>Largest gap (5-min ticks) bridged when interpolating falling series. Legacy: 24.</summary>
    public int FallingInterpolationGap { get; init; } = 24;

    /// <summary>Allowed end/start ratio for rising interpolation across the larger gap. Legacy: 1.25.</summary>
    public double InterpolationRatio { get; init; } = 1.25;
}
