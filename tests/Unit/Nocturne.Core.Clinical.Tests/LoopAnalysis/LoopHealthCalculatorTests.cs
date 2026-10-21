using Nocturne.Core.Clinical.LoopAnalysis;
using Nocturne.Core.Models;
using Nocturne.Core.Models.LoopAnalysis;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Clinical.Tests.LoopAnalysis;

public class LoopHealthCalculatorTests
{
    // Sydney in June is UTC+10 with no DST.
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
    private const long Minute = 60_000;
    private const long Hour = 60 * Minute;

    /// <summary>Local midnight starting 2024-06-10 in Sydney.</summary>
    private static readonly long Day0 = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.FromHours(10)).ToUnixTimeMilliseconds();

    private static LoopHealthRange Calculate(
        long from,
        long to,
        IEnumerable<ApsLoopCycle>? cycles = null,
        IEnumerable<DeliveredBasalSegment>? delivered = null,
        IEnumerable<RateSegment>? scheduled = null,
        IEnumerable<InsulinDose>? doses = null,
        IntervalSet? recorded = null,
        StepTimeline<double>? loopMaximum = null,
        LoopAnalysisMasks? masks = null,
        LoopAnalysisMaskSources? sources = null)
    {
        var scheduledList = (scheduled ?? []).ToList();
        var inputs = new LoopHealthInputs
        {
            Zone = Sydney,
            Cycles = [.. cycles ?? []],
            Insulin = new NetInsulinAboveSchedule(delivered ?? [], scheduledList, doses ?? []),
            Evidence = new BasalEvidence(recorded ?? IntervalSet.Empty, IntervalSet.Empty, IntervalSet.Empty),
            Scheduled = scheduledList,
            LoopMaximumBasalRate = loopMaximum,
        };
        var mask = LoopAnalysisMask.Build(
            masks ?? new LoopAnalysisMasks { ExcludeIllness = false, ExcludeOpenLoop = false },
            from, to, Sydney, sources ?? new LoopAnalysisMaskSources());
        return LoopHealthCalculator.Calculate(inputs, mask, from, to);
    }

    private static ApsLoopCycle Cycle(
        long mills,
        AidAlgorithm algorithm = AidAlgorithm.Trio,
        bool enacted = true,
        double? rate = null,
        string? limitReason = null,
        bool hasReason = true,
        double? iob = null,
        double? sensitivity = null,
        bool? suggestedChange = true,
        double? maxIob = null,
        double? maxBasal = null,
        double? daily = null,
        double? current = null) => new()
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime,
            AidAlgorithm = algorithm,
            Enacted = enacted,
            EnactedRate = rate,
            EnactedDuration = rate is null ? null : 30,
            Iob = iob,
            SensitivityRatio = sensitivity,
            HasSuggestion = algorithm != AidAlgorithm.Loop,
            HasReason = algorithm != AidAlgorithm.Loop && hasReason,
            SuggestedChange = algorithm != AidAlgorithm.Loop ? suggestedChange : null,
            LimitReason = limitReason,
            MaxIob = maxIob,
            MaxBasal = maxBasal,
            MaxDailySafetyMultiplier = daily,
            CurrentBasalSafetyMultiplier = current,
        };

    private static IEnumerable<ApsLoopCycle> EveryFiveMinutes(long from, long to, Func<long, ApsLoopCycle> cycle)
    {
        for (var t = from; t < to; t += 5 * Minute)
            yield return cycle(t);
    }

    [Fact]
    public void OrefReason_ReadsTheCapsItNames()
    {
        var capped = OrefReason.Parse("Eventual BG 180 >= 100, insulinReq 0.9. Microbolusing 0.4U. adj. req. rate: 3.2 to maxSafeBasal: 2.4, temp 1.2 < 2.4U/hr.");
        capped.Should().Be(new OrefReason(true, 2.4, false, null));

        OrefReason.Parse("Eventual BG 200 >= 100, IOB 3.21 > max_iob 3; setting current basal of 0.8 as temp.")
            .Should().Be(new OrefReason(false, null, true, 3));
        OrefReason.Parse("Eventual BG 200 >= 100, max_iob 3.5,  insulinReq 0.4; maxBolus 1")
            .Should().Be(new OrefReason(false, null, true, 3.5));
        OrefReason.Parse(null).Should().Be(default(OrefReason));
    }

    [Fact]
    public void UnknownLimits_AreReportedUnknownRatherThanDefaulted()
    {
        var from = Day0;
        var to = Day0 + 2 * Hour;
        var result = Calculate(from, to, cycles:
        [
            .. EveryFiveMinutes(from, from + Hour, t => Cycle(t, rate: 4.0, hasReason: false)),
            .. EveryFiveMinutes(from + Hour, to, t => Cycle(t, AidAlgorithm.Loop, rate: 4.0)),
        ]);

        var maxBasal = result.MaxBasal!;
        maxBasal.AtCap.Percent.Should().BeNull("no cycle's cap is known: no reason to read and no Loop setting");
        maxBasal.CapKnown.Percent.Should().Be(0);
        maxBasal.Binding.Should().BeEmpty();
        result.AtMaxIob.Should().BeNull("only oref cycles with a reason say whether max_iob bound");
        result.Limits.Should().BeEquivalentTo(new LoopHealthLimits());
    }

    [Fact]
    public void OrefWithoutRecordedLimits_TakesTheCapItsReasonReports()
    {
        var from = Day0;
        var to = Day0 + Hour;
        var result = Calculate(from, to, cycles: EveryFiveMinutes(from, to, t => t < from + 15 * Minute
            ? Cycle(t, rate: 2.4, limitReason: "adj. req. rate: 3.2 to maxSafeBasal: 2.4, temp 1.2 < 2.4U/hr.")
            : Cycle(t, rate: 1.0)));

        var maxBasal = result.MaxBasal!;
        maxBasal.CapKnown.Percent.Should().Be(100, "a reason that names no cap says none was cut");
        maxBasal.AtCap.Percent.Should().Be(25);
        maxBasal.Binding.Should().ContainSingle().Which.Should().BeEquivalentTo(new LoopHealthCapShare
        {
            Cap = LoopHealthCap.OrefMaxSafeBasal, Minutes = 15, Percent = 100,
        });
        result.Limits.OrefMaxBasal.Should().BeNull();
    }

    [Theory]
    [InlineData(1.5, 3, 4, 1.5, LoopHealthCap.OrefMaxBasal)]
    [InlineData(5.0, 2, 5, 2.0, LoopHealthCap.OrefMaxDailySafetyMultiplier)]
    [InlineData(5.0, 3, 2, 1.0, LoopHealthCap.OrefCurrentBasalSafetyMultiplier)]
    public void OrefWithRecordedLimits_NamesTheLowestCapAsBinding(
        double maxBasal, double daily, double current, double cap, LoopHealthCap binding)
    {
        // The schedule runs 0.5 U/h now and peaks at 1.0 U/h later in the day.
        var from = Day0 + Hour;
        var to = from + Hour;
        RateSegment[] schedule = [new(Day0, Day0 + 12 * Hour, 0.5), new(Day0 + 12 * Hour, Day0 + 24 * Hour, 1.0)];
        var result = Calculate(from, to, scheduled: schedule, cycles: EveryFiveMinutes(from, to, t =>
            Cycle(t, rate: t < from + 30 * Minute ? cap : cap / 2, maxBasal: maxBasal, daily: daily, current: current)));

        result.MaxBasal!.AtCap.Percent.Should().Be(50);
        result.MaxBasal.Binding.Should().ContainSingle().Which.Cap.Should().Be(binding);
        result.Limits.OrefMaxBasal.Should().Be(maxBasal);
        result.Limits.OrefMaxDailySafetyMultiplier.Should().Be(daily);
        result.Limits.OrefCurrentBasalSafetyMultiplier.Should().Be(current);
    }

    [Fact]
    public void Loop_IsAtItsMaximumBasalRateSetting()
    {
        var from = Day0;
        var to = Day0 + Hour;
        var maximum = StepTimeline<double>.LatestStartedWins([new Step<double>(Day0 - Hour, to, 2.0)]);
        var result = Calculate(from, to, loopMaximum: maximum, cycles: EveryFiveMinutes(from, to, t =>
            Cycle(t, AidAlgorithm.Loop, rate: t < from + 45 * Minute ? 1.98 : 1.0)));

        result.MaxBasal!.AtCap.Percent.Should().Be(75, "1.98 U/h is within a pump step of the 2.0 U/h maximum");
        result.MaxBasal.Binding.Should().ContainSingle().Which.Cap.Should().Be(LoopHealthCap.LoopMaximumBasalRate);
        result.Limits.LoopMaximumBasalRate.Should().Be(2.0);
        result.AtMaxIob.Should().BeNull();
        result.SuggestedNotEnacted.Should().BeNull("Loop records no suggestion apart from what it enacts");
    }

    [Fact]
    public void AGapAcrossMidnight_CountsOnce()
    {
        var from = Day0;
        var to = Day0 + 48 * Hour;
        var lastBefore = Day0 + 23 * Hour + 30 * Minute;
        var firstAfter = Day0 + 25 * Hour;
        var result = Calculate(from, to, cycles:
        [
            .. EveryFiveMinutes(from, lastBefore + Minute, t => Cycle(t)),
            .. EveryFiveMinutes(firstAfter, to, t => Cycle(t)),
        ]);

        var gaps = result.Cadence.LoopGaps!;
        gaps.Count.Should().Be(1);
        gaps.TotalMinutes.Should().Be(90);
        gaps.LongestMinutes.Should().Be(90);
        gaps.CountByStartHour[23].Should().Be(1);
        gaps.CountByStartHour.Sum().Should().Be(1);
        gaps.Gaps.Single().Date.Should().Be("2024-06-10");
        gaps.Gaps.Single().StartMinuteOfDay.Should().Be(23 * 60 + 30);
        gaps.Time.Hours[0].Percent.Should().Be(50, "the gap filled 00:00-01:00 on one of the two days");
        gaps.Time.Hours[23].Percent.Should().Be(25);
    }

    [Fact]
    public void AGapThatStraddlesTheRangeIsClippedToIt()
    {
        var from = Day0 + Hour;
        var to = Day0 + 3 * Hour;
        var result = Calculate(from, to, cycles: [Cycle(Day0), Cycle(Day0 + 2 * Hour), Cycle(Day0 + 2 * Hour + 5 * Minute)]);

        result.Cadence.LoopGaps!.Gaps.Single().Minutes.Should().Be(60);
        result.Cadence.LoopGaps.Count.Should().Be(1);
    }

    [Fact]
    public void ZeroDelivery_IsSplitByOrigin()
    {
        var from = Day0;
        var to = Day0 + 4 * Hour;
        var result = Calculate(from, to, delivered:
        [
            new(from, from + Hour, 0, BasalDeliveryOrigin.Algorithm),
            new(from + Hour, from + 2 * Hour, 0, BasalDeliveryOrigin.Manual),
            new(from + 2 * Hour, from + 3 * Hour, 0, BasalDeliveryOrigin.Suspended),
            new(from + 3 * Hour, to, 1.0, BasalDeliveryOrigin.Algorithm),
        ]);

        result.ZeroDelivery!.AlgorithmZeroTemp.Percent.Should().Be(25);
        result.ZeroDelivery.ManualZeroTemp.Percent.Should().Be(25);
        result.ZeroDelivery.Suspended.Percent.Should().Be(25);
        result.ZeroDelivery.AlgorithmZeroTemp.Hours[0].Percent.Should().Be(100);
        result.ZeroDelivery.Suspended.Hours[2].Percent.Should().Be(100);
        result.ZeroDelivery.Suspended.Hours[5].Percent.Should().BeNull("delivery is unknown there");
    }

    [Fact]
    public void AutomaticBoluses_AreCountedPerDayAndSized()
    {
        var from = Day0;
        var to = Day0 + 2 * 24 * Hour;
        var result = Calculate(from, to, doses:
        [
            new(Day0 + 3 * Hour, 0.2, true),
            new(Day0 + 3 * Hour + 10 * Minute, 0.4, true),
            new(Day0 + 27 * Hour, 0.6, true),
            new(Day0 + 8 * Hour, 3.0, false),
        ]);

        var boluses = result.AutomaticBoluses!;
        boluses.PerDay.Overall!.Mean.Should().Be(1.5);
        boluses.PerDay.Overall.N.Should().Be(2);
        boluses.UnitsPerDay.Overall!.Mean.Should().Be(0.6);
        boluses.Size.Overall!.P50.Should().Be(0.4);
        boluses.Size.Overall.N.Should().Be(3);
        boluses.PerDay.Hours[3].Value!.Mean.Should().Be(1.5);
        boluses.PerDay.Hours[8].Value!.Mean.Should().Be(0, "a manual bolus is not an automatic one");
    }

    [Fact]
    public void SuggestedNotEnacted_IsTheShareOfChangesAskedForAndNotMade()
    {
        var from = Day0;
        var to = Day0 + Hour;
        var result = Calculate(from, to, cycles:
        [
            Cycle(from, enacted: true),
            Cycle(from + 5 * Minute, enacted: false),
            Cycle(from + 10 * Minute, enacted: true),
            Cycle(from + 15 * Minute, enacted: true),
            Cycle(from + 20 * Minute, enacted: false, suggestedChange: false),
        ]);

        result.SuggestedNotEnacted!.Percent.Should().Be(25);
        result.SuggestedNotEnacted.Hours[0].Percent.Should().Be(25);
    }

    [Fact]
    public void AtMaxIob_ReadsTheReasonAndThenIobNearTheReportedLimit()
    {
        var from = Day0;
        var to = Day0 + Hour;
        var result = Calculate(from, to, cycles: EveryFiveMinutes(from, to, t =>
            t == from
                ? Cycle(t, limitReason: "Eventual BG 200 >= 100, max_iob 3,  insulinReq 0.4", iob: 2.6)
                : Cycle(t, iob: t < from + 30 * Minute ? 2.95 : 1.0)));

        result.AtMaxIob!.Percent.Should().Be(50);
        result.Limits.OrefMaxIob.Should().Be(3);
    }

    [Fact]
    public void SensitivityRatio_IsPooledByHour()
    {
        var from = Day0;
        var to = Day0 + 2 * Hour;
        var result = Calculate(from, to, cycles: EveryFiveMinutes(from, to, t =>
            Cycle(t, sensitivity: t < from + Hour ? 0.8 : 1.2)));

        result.SensitivityRatio!.Hours[0].Value!.P50.Should().Be(0.8);
        result.SensitivityRatio.Hours[1].Value!.P50.Should().Be(1.2);
        result.SensitivityRatio.Overall!.N.Should().Be(24);
        result.SensitivityRatio.Overall.Sparse.Should().BeTrue();
    }

    [Fact]
    public void UnrecordedDelivery_FindsALoopRunningWithNoTempRecords()
    {
        var from = Day0;
        var to = Day0 + 6 * Hour;
        var result = Calculate(from, to,
            cycles: EveryFiveMinutes(from, to + 5 * Minute, t => Cycle(t)),
            recorded: IntervalSet.Of(from, from + Hour));

        var gap = result.Cadence.UnrecordedDelivery!.Gaps.Should().ContainSingle().Subject;
        gap.Minutes.Should().Be(300);
        result.Cadence.LoopGaps!.Count.Should().Be(0);
    }

    [Fact]
    public void Activity_CreditsFiveMinutesPerCycle()
    {
        var from = Day0;
        var to = Day0 + 2 * Hour;
        var result = Calculate(from, to, cycles: EveryFiveMinutes(from, from + Hour, t =>
            Cycle(t, enacted: (t - from) / (5 * Minute) % 2 == 0)));

        result.Activity.Pump!.Hours[0].Percent.Should().Be(100);
        result.Activity.ClosedLoop!.Hours[0].Percent.Should().Be(50);
        result.Activity.ClosedLoop.Hours[1].Percent.Should().Be(0);
        result.Activity.ClosedLoop.Percent.Should().Be(25);
    }

    [Fact]
    public void FallBackDay_WeighsTheRepeatedHourByItsLength()
    {
        // 2024-04-07 in Sydney is 25 hours long: 03:00 AEDT falls back to 02:00 AEST.
        var from = new DateTimeOffset(2024, 4, 6, 13, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var to = new DateTimeOffset(2024, 4, 7, 14, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var secondTwoAm = new DateTimeOffset(2024, 4, 6, 16, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var result = Calculate(from, to,
            cycles: EveryFiveMinutes(from, to, t => Cycle(t)),
            doses: [new(secondTwoAm + Minute, 0.2, true)]);

        result.Days.Should().ContainSingle();
        result.Activity.Pump!.Hours[2].Percent.Should().Be(100);
        result.Activity.Pump.Hours[2].Daily!.N.Should().Be(1);
        result.AutomaticBoluses!.PerDay.Hours[2].Value!.Mean.Should().Be(0.5, "one bolus over the two hours 02:00 ran for");
        result.AutomaticBoluses.PerDay.Overall!.Mean.Should().Be(1);
    }

    [Fact]
    public void AMaskedHour_IsLeftOutOfEveryFigure()
    {
        var from = Day0;
        var to = Day0 + 2 * Hour;
        var result = Calculate(from, to,
            cycles: EveryFiveMinutes(from, to, t => Cycle(t, sensitivity: 1.0)),
            masks: new LoopAnalysisMasks { ExcludeIllness = true, ExcludeOpenLoop = false },
            sources: new LoopAnalysisMaskSources { Illness = [new MillsInterval(from, from + Hour)] });

        result.Activity.Pump!.Hours[0].Percent.Should().BeNull();
        result.Activity.Pump.Hours[1].Percent.Should().Be(100);
        result.SensitivityRatio!.Overall!.N.Should().Be(12);
    }
}
