using Microsoft.AspNetCore.Mvc;
using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.API.Controllers.V4.Analytics;

/// <summary>
/// The Meals &amp; events cohorts and window, bound beside <see cref="LoopAnalysisQuery"/> as
/// camelCase scalars. Each of up to <see cref="EventResponseDefinition.MaxCohorts"/> cohorts is
/// its parameters suffixed with its number: <c>anchor1</c>, <c>fromMinute1</c>, <c>minGrams1</c>.
/// With no anchor named, cohort 1 is every meal.
/// </summary>
public sealed class EventResponseQuery
{
    [FromQuery(Name = "anchor1")] public EventAnchorKind? Anchor1 { get; set; }
    [FromQuery(Name = "anchor2")] public EventAnchorKind? Anchor2 { get; set; }
    [FromQuery(Name = "anchor3")] public EventAnchorKind? Anchor3 { get; set; }

    /// <summary>Start of cohort 1's local time-of-day window, minutes after midnight; see <see cref="EventCohortDefinition.FromMinute"/>.</summary>
    [FromQuery(Name = "fromMinute1")] public int? FromMinute1 { get; set; }
    [FromQuery(Name = "fromMinute2")] public int? FromMinute2 { get; set; }
    [FromQuery(Name = "fromMinute3")] public int? FromMinute3 { get; set; }

    /// <summary>End of cohort 1's window, exclusive; before the start, the window wraps past midnight.</summary>
    [FromQuery(Name = "toMinute1")] public int? ToMinute1 { get; set; }
    [FromQuery(Name = "toMinute2")] public int? ToMinute2 { get; set; }
    [FromQuery(Name = "toMinute3")] public int? ToMinute3 { get; set; }

    /// <summary>Smallest meal in cohort 1, g; for a meal cohort only.</summary>
    [FromQuery(Name = "minGrams1")] public double? MinGrams1 { get; set; }
    [FromQuery(Name = "minGrams2")] public double? MinGrams2 { get; set; }
    [FromQuery(Name = "minGrams3")] public double? MinGrams3 { get; set; }

    /// <summary>Largest meal in cohort 1, g; for a meal cohort only.</summary>
    [FromQuery(Name = "maxGrams1")] public double? MaxGrams1 { get; set; }
    [FromQuery(Name = "maxGrams2")] public double? MaxGrams2 { get; set; }
    [FromQuery(Name = "maxGrams3")] public double? MaxGrams3 { get; set; }

    /// <summary>Drop events with another meal or correction bolus in their window; ignored without treatments.read.</summary>
    [FromQuery(Name = "isolatedOnly")] public bool IsolatedOnly { get; set; }

    /// <summary>Count an unannounced rise only where automatic boluses answered it; ignored without treatments.read.</summary>
    [FromQuery(Name = "requireBolusCluster")] public bool RequireBolusCluster { get; set; }

    /// <summary>Minutes of window before the anchor: 0 to 120, a multiple of 5.</summary>
    [FromQuery(Name = "minutesBefore")] public int MinutesBefore { get; set; } = EventResponseDefinition.DefaultMinutesBefore;

    /// <summary>Minutes of window after the anchor: 60 to 480, a multiple of 5.</summary>
    [FromQuery(Name = "minutesAfter")] public int MinutesAfter { get; set; } = EventResponseDefinition.DefaultMinutesAfter;

    private const int MinutesPerDay = 24 * 60;

    private IEnumerable<(int Cohort, EventAnchorKind? Anchor, int? From, int? To, double? MinGrams, double? MaxGrams)> Cohorts() =>
    [
        (1, Anchor1, FromMinute1, ToMinute1, MinGrams1, MaxGrams1),
        (2, Anchor2, FromMinute2, ToMinute2, MinGrams2, MaxGrams2),
        (3, Anchor3, FromMinute3, ToMinute3, MinGrams3, MaxGrams3),
    ];

    /// <summary>The cohort's anchor: the one named, or every meal for cohort 1 when none is.</summary>
    private EventAnchorKind? KindOf(int cohort, EventAnchorKind? anchor) =>
        anchor ?? (cohort == 1 && Anchor1 is null && Anchor2 is null && Anchor3 is null ? EventAnchorKind.Meal : null);

    /// <summary>Why the parameters do not make a definition, or null when they do.</summary>
    public string? Problem()
    {
        if (MinutesBefore is < 0 or > EventResponseDefinition.MaxMinutesBefore || MinutesBefore % EventResponseDefinition.SlotMinutes != 0)
            return $"minutesBefore must be a multiple of 5 from 0 to {EventResponseDefinition.MaxMinutesBefore}.";
        if (MinutesAfter is < EventResponseDefinition.MinMinutesAfter or > EventResponseDefinition.MaxMinutesAfter
            || MinutesAfter % EventResponseDefinition.SlotMinutes != 0)
            return $"minutesAfter must be a multiple of 5 from {EventResponseDefinition.MinMinutesAfter} to {EventResponseDefinition.MaxMinutesAfter}.";

        foreach (var (n, anchor, from, to, min, max) in Cohorts())
        {
            var kind = KindOf(n, anchor);
            if (kind is null && (from ?? to ?? min ?? max) is not null)
                return $"Cohort {n}'s filters need anchor{n}.";
            if (from is < 0 or > MinutesPerDay || to is < 0 or > MinutesPerDay)
                return $"fromMinute{n} and toMinute{n} must be minutes of the day, 0 to {MinutesPerDay}.";
            if (from is not null && from == to)
                return $"fromMinute{n} and toMinute{n} must differ.";
            if ((min ?? max) is not null && kind != EventAnchorKind.Meal)
                return $"minGrams{n} and maxGrams{n} apply only to a meal cohort.";
            if (min is < 0 || max is < 0 || min > max)
                return $"minGrams{n} and maxGrams{n} must be grams, the first no more than the second.";
        }
        return null;
    }

    /// <summary>The definition asked for, before the caller's scopes narrow it.</summary>
    public EventResponseDefinition ToDefinition() => new()
    {
        Cohorts =
        [
            .. Cohorts()
                .Select(c => c with { Anchor = KindOf(c.Cohort, c.Anchor) })
                .Where(c => c.Anchor is not null)
                .Select(c => new EventCohortDefinition
                {
                    Cohort = c.Cohort,
                    Anchor = c.Anchor!.Value,
                    FromMinute = c.From,
                    ToMinute = c.To,
                    MinGrams = c.MinGrams,
                    MaxGrams = c.MaxGrams,
                }),
        ],
        MinutesBefore = MinutesBefore,
        MinutesAfter = MinutesAfter,
        IsolatedOnly = IsolatedOnly,
        RequireBolusCluster = RequireBolusCluster,
    };
}
