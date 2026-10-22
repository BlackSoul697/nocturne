using Nocturne.Core.Clinical.LoopAnalysis;
using Nocturne.Core.Clinical.Treatments;
using Nocturne.Core.Models.LoopAnalysis;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Clinical.Tests.LoopAnalysis;

public class EventResponseCalculatorTests
{
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
    private const long Minute = 60_000;
    private const long Hour = 60 * Minute;

    private static long Local(int y, int mo, int d, int h, int mi = 0) =>
        new DateTimeOffset(new DateTime(y, mo, d, h, mi, 0), Sydney.GetUtcOffset(new DateTime(y, mo, d, h, mi, 0)))
            .ToUnixTimeMilliseconds();

    // A June week in Sydney, UTC+10 with no DST.
    private static readonly long From = Local(2024, 6, 10, 0);
    private static readonly long To = Local(2024, 6, 17, 0);

    private static EventCohortDefinition Cohort(
        int n, EventAnchorKind anchor = EventAnchorKind.Meal, int? from = null, int? to = null, double? minGrams = null, double? maxGrams = null) =>
        new() { Cohort = n, Anchor = anchor, FromMinute = from, ToMinute = to, MinGrams = minGrams, MaxGrams = maxGrams };

    private static EventResponseRange Calculate(
        IEnumerable<EventCohortDefinition> cohorts,
        IEnumerable<GlucoseSample>? glucose = null,
        IEnumerable<CarbEvent>? carbs = null,
        IEnumerable<InsulinDose>? doses = null,
        IEnumerable<DeliveredBasalSegment>? delivered = null,
        IEnumerable<RateSegment>? scheduled = null,
        IEnumerable<IobCobTick>? iobCob = null,
        LoopAnalysisMaskSources? sources = null,
        bool isolatedOnly = false,
        long? from = null,
        long? to = null)
    {
        var definition = new EventResponseDefinition { Cohorts = [.. cohorts], IsolatedOnly = isolatedOnly };
        var inputs = new EventResponseInputs
        {
            Series = new DailyProfileInputs
            {
                Zone = Sydney,
                Insulin = new NetInsulinAboveSchedule(delivered ?? [], scheduled ?? [], doses ?? []),
                Glucose = [.. glucose ?? []],
                Carbs = [.. carbs ?? []],
                IobCob = [.. iobCob ?? []],
            },
            Sources = sources ?? new LoopAnalysisMaskSources(),
        };
        var start = from ?? From;
        var end = to ?? To;
        var mask = LoopAnalysisMask.Build(new LoopAnalysisMasks(), start, end, Sydney, inputs.Sources);
        return EventResponseCalculator.Calculate(inputs, mask, start, end, definition);
    }

    private static IEnumerable<GlucoseSample> Readings(long from, long to, Func<long, double> mgdl)
    {
        for (var t = from; t < to; t += 5 * Minute)
            yield return new GlucoseSample(t, mgdl(t));
    }

    private static IEnumerable<GlucoseSample> Flat(double mgdl) =>
        Readings(From - 12 * Hour, To + 12 * Hour, _ => mgdl);

    [Fact]
    public void AMealAtMidnight_IsAnchoredOnTheDayItStarts()
    {
        var midnight = Local(2024, 6, 12, 0);

        var range = Calculate([Cohort(1, from: 0, to: 60)], glucose: Flat(120), carbs: [new(midnight, 30)]);

        var cohort = range.Cohorts.Single();
        cohort.Events.Should().Be(1);
        cohort.Slots.Should().HaveCount(72);
        cohort.Slots[12].Minute.Should().Be(0);
        cohort.Slots[0].Minute.Should().Be(-60);
        cohort.Slots[12].GlucoseChange!.P50.Should().Be(0);
        var e = range.Events.Single();
        e.Date.Should().Be("2024-06-12");
        e.Time.Should().Be("00:00");
        e.Grams.Should().Be(30);
        e.Cohorts.Should().Equal(1);
    }

    [Fact]
    public void AnchorsNearMidnight_ReadTheirWindowPastTheRangeAndWrapTheTimeOfDayWindow()
    {
        var lastEvening = Local(2024, 6, 16, 23, 55);
        var justAfter = Local(2024, 6, 13, 0, 30);
        var breakfast = Local(2024, 6, 13, 7, 0);
        // Glucose rises an hour after the last anchor, which is past the range's end.
        var glucose = Flat(120).Select(g => g.Mills >= lastEvening + Hour ? g with { Mgdl = 180 } : g);

        var range = Calculate(
            [Cohort(1, from: 22 * 60, to: 2 * 60)],
            glucose: glucose,
            carbs: [new(lastEvening, 20), new(justAfter, 20), new(breakfast, 20)]);

        range.Cohorts.Single().Events.Should().Be(2, "22:00-02:00 wraps midnight and leaves 07:00 out");
        var last = range.Events.Single(e => e.At == DateTimeOffset.FromUnixTimeMilliseconds(lastEvening).UtcDateTime);
        last.Date.Should().Be("2024-06-16");
        last.Time.Should().Be("23:55");
        last.Metrics.PeakRise.Should().Be(60);
        last.Metrics.GlucoseAt2Hours.Should().Be(180);
    }

    [Fact]
    public void OverlappingCohorts_CountAnEventInEach_AndListItOnce()
    {
        var breakfast = Local(2024, 6, 11, 7, 0);
        var smallBreakfast = Local(2024, 6, 12, 7, 0);
        var dinner = Local(2024, 6, 11, 19, 0);

        var range = Calculate(
            [Cohort(1, minGrams: 40), Cohort(2, from: 5 * 60, to: 10 * 60)],
            glucose: Flat(120),
            carbs: [new(breakfast, 50), new(smallBreakfast, 20), new(dinner, 60)]);

        range.Cohorts.Select(c => c.Events).Should().Equal(2, 2);
        range.Events.Should().HaveCount(3);
        range.Events.Single(e => e.Grams == 50).Cohorts.Should().Equal(1, 2);
        range.Events.Single(e => e.Grams == 20).Cohorts.Should().Equal(2);
        range.Events.Single(e => e.Grams == 60).Cohorts.Should().Equal(1);
    }

    [Fact]
    public void AWindowAcrossAFallBack_StepsInAbsoluteTime()
    {
        // 2024-04-07 in Sydney: 03:00 AEDT falls back to 02:00 AEST, so local 01:30 plus 300
        // minutes is local 05:30, not 06:30.
        var from = new DateTimeOffset(2024, 4, 6, 13, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var to = from + 25 * Hour;
        var anchor = from + 90 * Minute;
        var glucose = Readings(from - 2 * Hour, to + 6 * Hour, t => 100 + (t - anchor) / (double)Minute);

        var range = Calculate([Cohort(1, from: 60, to: 120)], glucose: glucose, carbs: [new(anchor, 40)], from: from, to: to);

        var cohort = range.Cohorts.Single();
        cohort.Events.Should().Be(1);
        range.Events.Single().Time.Should().Be("01:30");
        cohort.Slots.Should().HaveCount(72);
        cohort.Slots.Select(s => s.GlucoseChange!.P50).Should().Equal(
            Enumerable.Range(0, 72).Select(k => (double)(k * 5 - 60)),
            "each slot is five minutes of absolute time from the anchor, across the transition");
        range.Events.Single().Metrics.GlucoseAt4Hours.Should().Be(340);
    }

    [Fact]
    public void IsolatedOnly_DropsAnEventWithAnotherMealOrCorrectionInItsWindow()
    {
        var first = Local(2024, 6, 11, 12, 0);
        var second = first + 2 * Hour;
        var correction = Local(2024, 6, 13, 12, 0);
        var alone = Local(2024, 6, 15, 12, 0);
        var mealBolus = new InsulinDose(alone - 10 * Minute, 4, false);

        var range = Calculate(
            [Cohort(1)],
            glucose: Flat(120),
            carbs: [new(first, 40), new(second, 40), new(correction - 30 * Minute, 40), new(alone, 40)],
            doses: [mealBolus, new(correction + 2 * Hour, 1, false)],
            isolatedOnly: true);

        var cohort = range.Cohorts.Single();
        cohort.Anchors.Should().Be(4);
        cohort.NotIsolated.Should().Be(2, "the first meal has the second in its window, the third a correction bolus");
        cohort.Events.Should().Be(2, "a meal's own bolus is not another event, and a meal 2 h before is outside the 60-minute lead");
        range.Events.Select(e => e.At).Should().Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(second).UtcDateTime,
            DateTimeOffset.FromUnixTimeMilliseconds(alone).UtcDateTime);
    }

    [Fact]
    public void AnEventWithoutAnAnchorGlucose_IsDroppedAndCounted()
    {
        var meal = Local(2024, 6, 11, 12, 0);
        var glucose = Flat(120).Where(g => Math.Abs(g.Mills - meal) > 5 * Minute);

        var cohort = Calculate([Cohort(1)], glucose: glucose, carbs: [new(meal, 40)]).Cohorts.Single();

        cohort.Anchors.Should().Be(1);
        cohort.MissingAnchorGlucose.Should().Be(1);
        cohort.Events.Should().Be(0);
        cohort.Slots.Should().OnlyContain(s => s.Glucose == null);
    }

    [Fact]
    public void AMealsFiguresAreReadOffItsWindow()
    {
        var meal = Local(2024, 6, 11, 12, 0);
        // 100 at the meal, up 80 by +60, back down to 110 by +180 and staying there.
        double Shape(long t) => ((t - meal) / (double)Minute) switch
        {
            var m and < 0 => 100,
            var m and <= 60 => 100 + m * 80 / 60,
            var m and <= 180 => 180 - (m - 60) * 70 / 120,
            _ => 110,
        };

        var range = Calculate(
            [Cohort(1)],
            glucose: Readings(From, To, Shape),
            carbs: [new(meal, 60)],
            doses: [new(meal, 4, false), new(meal + 30 * Minute, 1, true)],
            delivered: [new(From, To, 1.5, BasalDeliveryOrigin.Algorithm)],
            scheduled: [new(From, To, 1.0)],
            iobCob: [new(meal - 5 * Minute, 0.5, IobCobSource.Device, 0, IobCobSource.Device), new(meal, 4.5, IobCobSource.Device, 60, IobCobSource.Device)]);

        var m = range.Events.Single().Metrics;
        m.StartGlucose.Should().Be(100);
        m.PeakRise.Should().Be(80);
        m.MinutesToPeak.Should().Be(60);
        m.GlucoseAt2Hours.Should().Be(145);
        m.MinutesBackToStart.Should().Be(165, "the first reading within 20 of the start after the peak is 120 at +165");
        m.TotalInsulin.Should().Be(14, "1.5 U/h for six hours and 5 U of boluses");
        m.NetInsulin.Should().Be(8, "0.5 U/h above schedule for six hours and 5 U of boluses");
        m.GramsPerUnit.Should().Be(7.5);
        m.NetInsulinFirst30.Should().Be(4.25, "the meal bolus at the anchor and 0.5 U/h above schedule");
        m.NetInsulinFirst60.Should().Be(5.5);
        m.IobAtAnchor.Should().Be(4.5);
        m.IobAtEnd.Should().BeNull("no IOB reached the window's end");

        var slot = range.Cohorts.Single().Slots;
        slot[12].NetLoopCorrection!.P50.Should().Be(0.5);
        slot[18].NetLoopCorrection!.P50.Should().Be(12.5, "a 1 U automatic bolus in five minutes is 12 U/h on top");
        slot[11].NetInsulinSinceAnchor.Should().BeNull("before the anchor nothing has accrued since it");
        slot[12].NetInsulinSinceAnchor!.P50.Should().BeApproximately(4.042, 0.001);
        slot[12].NetLoopCorrectionSinceAnchor!.P50.Should().BeApproximately(0.042, 0.001);
        slot[12].Iob!.P50.Should().Be(4.5);
        slot[12].Cob!.P50.Should().Be(60);
    }

    [Fact]
    public void ACorrectionBolus_IsAManualBolusWithNoCarbsWithinHalfAnHour()
    {
        var correction = Local(2024, 6, 11, 15, 0);
        var mealBolus = Local(2024, 6, 12, 12, 0);

        var range = Calculate(
            [Cohort(1, EventAnchorKind.CorrectionBolus)],
            glucose: Flat(180),
            carbs: [new(mealBolus + 20 * Minute, 40)],
            doses: [new(correction, 2, false), new(mealBolus, 4, false), new(correction + Hour, 0.5, true)]);

        range.Events.Should().ContainSingle().Which.At.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(correction).UtcDateTime);
    }

    [Fact]
    public void SpanStarts_AnchorOnlyInsideTheRange()
    {
        var exercise = Local(2024, 6, 11, 17, 0);

        var range = Calculate(
            [Cohort(1, EventAnchorKind.ExerciseStart), Cohort(2, EventAnchorKind.SensorStart)],
            glucose: Flat(120),
            sources: new LoopAnalysisMaskSources
            {
                Exercise = [new(exercise, exercise + Hour), new(From - Hour, From + Hour)],
            });

        range.Cohorts[0].Events.Should().Be(1, "an exercise span that began before the range is not an anchor in it");
        range.Cohorts[1].Events.Should().Be(0);
    }

    [Fact]
    public void SensorStarts_LoggedTogether_AreOneSensor()
    {
        var change = Local(2024, 6, 11, 9, 0);

        EventAnchors.SensorStarts([change, change + 30 * Minute, change + 3 * Day()])
            .Select(a => a.Mills).Should().Equal(change, change + 3 * Day());

        static long Day() => 24 * Hour;
    }

    [Fact]
    public void AnUnannouncedRise_IsAnchoredAndMeasuredLikeAMeal()
    {
        var start = Local(2024, 6, 11, 13, 0);
        var glucose = Readings(From, To, t => t <= start ? 110 : Math.Min(200, 110 + 3 * (t - start) / (double)Minute));
        var smbs = Enumerable.Range(1, 6).Select(i => new InsulinDose(start + i * 5 * Minute, 0.3, true));

        var range = Calculate(
            [Cohort(1, EventAnchorKind.UnannouncedRise), Cohort(2)],
            glucose: glucose,
            doses: smbs,
            delivered: [new(From, To, 1.0, BasalDeliveryOrigin.Algorithm)],
            scheduled: [new(From, To, 1.0)]);

        var e = range.Events.Should().ContainSingle().Subject;
        e.Anchor.Should().Be(EventAnchorKind.UnannouncedRise);
        e.Metrics.PeakRise.Should().Be(90);
        e.Metrics.NetInsulinFirst30.Should().BeApproximately(1.5, 0.001, "five of the 0.3 U boluses land in the first half hour");
        e.Metrics.NetInsulinFirst60.Should().BeApproximately(1.8, 0.001);
        range.Cohorts[1].Anchors.Should().Be(0);
    }
}
