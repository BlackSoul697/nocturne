using Nocturne.Core.Clinical.LoopAnalysis;
using Nocturne.Core.Clinical.Treatments;
using Nocturne.Core.Models.Basal;
using Nocturne.Core.Models.LoopAnalysis;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Clinical.Tests.LoopAnalysis;

public class SettingsEvidenceCalculatorTests
{
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
    private const long Minute = 60_000;
    private const long Hour = 60 * Minute;
    private const long Day = 24 * Hour;

    private static long Local(int y, int mo, int d, int h, int mi = 0) =>
        new DateTimeOffset(new DateTime(y, mo, d, h, mi, 0), Sydney.GetUtcOffset(new DateTime(y, mo, d, h, mi, 0)))
            .ToUnixTimeMilliseconds();

    // June in Sydney, UTC+10 with no DST.
    private static readonly long June = Local(2024, 6, 3, 0);

    private static SettingsEvidenceRange Calculate(
        DailyProfileInputs series, TherapyTimeline therapy, long from, long to,
        LoopAnalysisMaskSources? sources = null, LoopAnalysisMasks? masks = null, bool listEpisodes = true)
    {
        var inputs = new SettingsEvidenceInputs { Series = series, Sources = sources ?? new(), Therapy = therapy };
        var mask = LoopAnalysisMask.Build(masks ?? new LoopAnalysisMasks(), from, to, Sydney, inputs.Sources);
        return SettingsEvidenceCalculator.Calculate(inputs, mask, from, to, listEpisodes);
    }

    private static TherapyTimeline Therapy(long from, long to, double isf = 40, double carbRatio = 10, double basal = 1) =>
        new([new TherapySegment(from, to, new TherapySnapshot(
            4, 75, 30, Sydney, null, 0,
            [new ScheduleEntry { TimeAsSeconds = 0, Value = isf }],
            [new ScheduleEntry { TimeAsSeconds = 0, Value = carbRatio }],
            [new ScheduleEntry { TimeAsSeconds = 0, Value = basal }]))]);

    #region Recovery from a simulated loop

    private const int SimulatedDays = 28;

    /// <summary>
    /// Four weeks of three meals a day, breakfast at 07:00 and dinner at 19:30 announced. On even days
    /// lunch at 12:00 is announced; on odd days a snack at 11:30 is eaten unannounced and corrected
    /// with a manual bolus at 15:30, which gives the midday block its ISF.
    /// </summary>
    private static LoopSimulation DayPlan(bool corrections = true)
    {
        var meals = new List<SimulatedMeal>();
        var boluses = new List<(long, double)>();
        for (var d = -1; d <= SimulatedDays; d++)
        {
            var day = June + d * Day;
            meals.Add(new(day + 7 * Hour, 40 + (d + 3) % 3 * 10));
            meals.Add(new(day + 19 * Hour + 30 * Minute, 50 + (d + 4) % 4 * 5));
            if ((d + 2) % 2 == 0)
                meals.Add(new(day + 12 * Hour, 45));
            else
            {
                meals.Add(new(day + 11 * Hour + 30 * Minute, 40, Announced: false));
                if (corrections)
                    boluses.Add((day + 15 * Hour + 30 * Minute, 3));
            }
        }
        return new LoopSimulation { Zone = Sydney, Meals = meals, Corrections = boluses };
    }

    /// <summary>
    /// Five weeks on a three-night cycle, so the overnight block has its own basal need, ISF and
    /// carb ratio: fasting nights, with breakfast at 10:00, give 02:00-08:00 its basal; on the next
    /// night a 02:15 meal gives a carb ratio; on the third an unannounced snack at 22:00 is corrected
    /// at 02:00. Dinner is at 19:00 throughout.
    /// </summary>
    private static LoopSimulation NightPlan(bool corrections = true)
    {
        var meals = new List<SimulatedMeal>();
        var boluses = new List<(long, double)>();
        for (var d = -1; d <= NightDays; d++)
        {
            var day = June + d * Day;
            meals.Add(new(day - 5 * Hour, 50 + (d + 4) % 4 * 5));
            switch ((d + 3) % 3)
            {
                case 0:
                    meals.Add(new(day + 10 * Hour, 40));
                    break;
                case 1:
                    meals.Add(new(day + 2 * Hour + 15 * Minute, 30 + (d + 3) % 3 * 10));
                    meals.Add(new(day + 7 * Hour, 40));
                    break;
                default:
                    if (corrections)
                    {
                        meals.Add(new(day - 2 * Hour, 40, Announced: false));
                        boluses.Add((day + 2 * Hour, 3));
                    }
                    meals.Add(new(day + 7 * Hour, 40));
                    break;
            }
        }
        return new LoopSimulation { Zone = Sydney, Meals = meals, Corrections = boluses };
    }

    private const int NightDays = 36;

    private static SettingsEvidenceRange Recover(LoopSimulation plan, int days = SimulatedDays)
    {
        var (from, to) = (June, June + days * Day);
        var sim = plan.Run(from - Day, to + Day);
        return Calculate(
            new DailyProfileInputs
            {
                Zone = sim.Zone,
                Glucose = sim.Glucose,
                Insulin = new NetInsulinAboveSchedule(sim.Delivered, sim.Schedule(from - 2 * Day, to + 2 * Day), sim.Doses),
                Carbs = sim.Carbs,
                IobCob = sim.IobCob,
                TargetAt = _ => sim.TargetRange,
            },
            sim.Therapy(from - 2 * Day, to + 2 * Day),
            from,
            to);
    }

    private static BasalEvidenceBlock BasalBlock(SettingsEvidenceRange range, int startHour) =>
        range.Basal.Single(b => b.StartHour == startHour);

    private static IsfEvidenceBlock Isf(SettingsEvidenceRange range, SettingsEvidenceBlock block) =>
        range.Isf.Single(b => b.Block == block);

    private static CarbRatioEvidenceBlock CarbRatio(SettingsEvidenceRange range, SettingsEvidenceBlock block) =>
        range.CarbRatio.Single(b => b.Block == block);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AProfileWrongInAllThreeSettings_IsSeenThrough_ByATempLooperAndAnSmbLooper(bool smb)
    {
        var range = Recover(DayPlan() with
        {
            TrueBasal = _ => 1.2, TrueIsf = 40, TrueCr = 8, ProfileBasal = 1.0, ProfileIsf = 50, ProfileCr = 10, Smb = smb,
        });

        foreach (var hour in new[] { 2, 4 })
        {
            var basal = BasalBlock(range, hour);
            basal.Profile.Should().Be(1.0);
            basal.Observed.Should().BeApproximately(1.2, 0.05, $"the true basal is 1.2 U/h at {hour:00}:00");
            basal.MeanNetLoopCorrection.Should().BeApproximately(0.2, 0.05,
                "the loop tops a short schedule up, by temp basal or by automatic bolus");
            basal.Consistency.Should().Be(1);
        }

        var isf = Isf(range, SettingsEvidenceBlock.Midday);
        isf.Profile.Should().Be(50);
        isf.Observed.Should().BeApproximately(40, 2);
        isf.BasalDependent.Should().BeTrue("the midday window borrows the overnight blocks' basal ratio");

        foreach (var block in new[] { SettingsEvidenceBlock.Morning, SettingsEvidenceBlock.Midday, SettingsEvidenceBlock.Evening })
        {
            var cr = CarbRatio(range, block);
            cr.Profile.Should().Be(10);
            cr.Observed.Should().BeApproximately(8, 0.5, $"the true carb ratio is 8 g/U at {block}");
        }
    }

    [Fact]
    public void AWrongProfileIsf_DoesNotPassIntoTheCarbRatio_WhereTheBlockObservesItsOwn()
    {
        SettingsEvidenceRange Run(bool corrections) => Recover(
            NightPlan(corrections) with { TrueIsf = 40, TrueCr = 8, ProfileIsf = 80, ProfileCr = 14 },
            NightDays);

        var observed = Run(corrections: true);
        var isf = Isf(observed, SettingsEvidenceBlock.Overnight);
        isf.Observed.Should().BeApproximately(40, 3);
        SettingsEvidenceCalculator.Lendable(isf).Should().BeTrue();
        var overnight = CarbRatio(observed, SettingsEvidenceBlock.Overnight);
        overnight.IsfDependent.Should().BeFalse();
        overnight.BasalDependent.Should().BeFalse("02:00 to 06:15 lies in blocks whose own basal need is observed");
        overnight.Observed.Should().BeApproximately(8, 0.5);

        var inherited = CarbRatio(Run(corrections: false), SettingsEvidenceBlock.Overnight);
        inherited.IsfDependent.Should().BeTrue();
        inherited.Confidence.Should().NotBe(SettingsEvidenceConfidence.High,
            "its glucose term and its basal excess, each under the material share, together move it past it");
        inherited.Episodes.Should().OnlyContain(e => e.Isf == 80);
        Math.Abs(inherited.Observed!.Value - 8).Should().BeGreaterThan(Math.Abs(overnight.Observed!.Value - 8) + 0.2,
            "converting the meals' glucose change at the profile's ISF of 80 carries its error into the ratio");
    }

    [Fact]
    public void ABasalNeedThatVariesThroughTheDay_FlagsTheFiguresThatBorrowItsMean()
    {
        // The night needs 1.25 U/h and the day 1.0, as the profile has it; only the night is fasting,
        // so the daytime windows borrow the night's ratio and overstate the basal need under them.
        var range = Recover(DayPlan() with { TrueBasal = h => h < 6 ? 1.25 : 1.0 });

        BasalBlock(range, 2).Observed.Should().BeApproximately(1.25, 0.05);
        foreach (var figure in new InsulinActedFigure[]
                 {
                     CarbRatio(range, SettingsEvidenceBlock.Morning),
                     CarbRatio(range, SettingsEvidenceBlock.Midday),
                     CarbRatio(range, SettingsEvidenceBlock.Evening),
                 })
        {
            figure.BasalDependent.Should().BeTrue();
            figure.ConfidenceReasons.Should().Contain(SettingsEvidenceConfidenceReason.BasalDependent);
            figure.Confidence.Should().NotBe(SettingsEvidenceConfidence.High);
            figure.Observed.Should().BeGreaterThan(11, "the night's higher need, lent to the day, is taken off the meal's insulin");
        }
    }

    [Fact]
    public void ABasalBlockStillSettlingFromDinner_IsSuppressedForItsTrend()
    {
        var range = Recover(DayPlan() with { TrueIsf = 40, TrueCr = 8, ProfileIsf = 80, ProfileCr = 14 });

        var block = BasalBlock(range, 2);
        block.UnavailableReason.Should().Be(SettingsEvidenceUnavailableReason.SteepTrend);
        block.Observed.Should().BeNull();
        block.GlucoseTrend!.Mean.Should().BeLessThan(-ObservedBasalNeed.MaxTrendMgdlPerHour);
        block.MeanNetLoopCorrection.Should().NotBeNull("the block's correction and trend are reported even when its need is not");
    }

    [Fact]
    public void AProfileThatIsRight_ReadsModerate_WithOnlyTheBorrowedBasalAgainstIt()
    {
        var range = Recover(DayPlan());

        foreach (var block in new[] { SettingsEvidenceBlock.Morning, SettingsEvidenceBlock.Midday, SettingsEvidenceBlock.Evening })
        {
            var cr = CarbRatio(range, block);
            cr.Observed.Should().BeApproximately(10, 0.3);
            cr.Confidence.Should().Be(SettingsEvidenceConfidence.Moderate, $"{block}: {string.Join(",", cr.ConfidenceReasons)}");
            cr.ConfidenceReasons.Should().Equal(SettingsEvidenceConfidenceReason.BasalDependent);
        }
    }

    public static TheoryData<string> Departures =>
        ["noise", "longModelDia", "shortModelDia", "slowAbsorption", "nightNeedsMore", "nightNeedsLess"];

    /// <summary>
    /// A body that departs from what the calculator assumes, with the profile right by day: CGM
    /// noise of ±8 mg/dL, an IOB curve half as long again or half as long as the body's insulin acts,
    /// meals that absorb over five hours, past the four-hour window, and a night that needs 1.25 or
    /// 0.7 U/h against a schedule of 1.0, which the day's windows borrow. Each figure must stay within
    /// an honest tolerance of the truth. One more than 15% off towards more insulin (a basal above the
    /// truth, an ISF or carb ratio below it) must be Low; one that far off the other way must not be High.
    /// </summary>
    [Theory]
    [MemberData(nameof(Departures))]
    public void ABodyThatDepartsFromTheModel_IsReadWithinTolerance_OrWithLowerConfidence(string departure)
    {
        var plan = DayPlan() with { TrueIsf = 50, TrueCr = 10, ProfileIsf = 50, ProfileCr = 10 };
        plan = departure switch
        {
            "noise" => plan with { NoiseMgdl = 8 },
            "longModelDia" => plan with { ModelDiaSteps = 72, ModelPeakSteps = 18 },
            "shortModelDia" => plan with { ModelDiaSteps = 24, ModelPeakSteps = 10 },
            "slowAbsorption" => plan with { AbsorptionSteps = 60 },
            "nightNeedsMore" => plan with { TrueBasal = h => h < 6 ? 1.25 : 1.0 },
            "nightNeedsLess" => plan with { TrueBasal = h => h < 6 ? 0.7 : 1.0 },
            _ => throw new ArgumentOutOfRangeException(nameof(departure)),
        };

        var range = Recover(plan);

        var figures = range.Basal.Select(b => (Figure: (SettingsEvidenceFigure)b, Truth: plan.TrueBasal(b.StartHour), HigherMeansMore: true))
            .Concat(range.Isf.Select(b => (Figure: (SettingsEvidenceFigure)b, Truth: 50.0, HigherMeansMore: false)))
            .Concat(range.CarbRatio.Select(b => (Figure: (SettingsEvidenceFigure)b, Truth: 10.0, HigherMeansMore: false)))
            .Where(f => f.Figure.Observed is not null)
            .ToList();
        figures.Should().NotBeEmpty();
        foreach (var (figure, truth, higherMeansMore) in figures)
        {
            var observed = figure.Observed!.Value;
            var error = Math.Abs(observed - truth) / truth;
            var because = $"{departure}: {figure.GetType().Name} read {observed} against {truth}, {error:P0} off, {figure.Confidence} [{string.Join(",", figure.ConfidenceReasons)}]";
            error.Should().BeLessThan(Tolerance[departure], because);
            if (error <= 0.15)
                continue;
            if (higherMeansMore ? observed > truth : observed < truth)
                figure.Confidence.Should().Be(SettingsEvidenceConfidence.Low, because);
            else
                figure.Confidence.Should().NotBe(SettingsEvidenceConfidence.High, because);
        }
    }

    private static readonly Dictionary<string, double> Tolerance = new()
    {
        ["noise"] = 0.05,
        ["longModelDia"] = 0.25,
        ["shortModelDia"] = 0.2,
        ["slowAbsorption"] = 0.3,
        ["nightNeedsMore"] = 0.35,
        ["nightNeedsLess"] = 0.3,
    };

    #endregion

    #region Directed scenarios

    /// <summary>
    /// A week of flat data: glucose at 120, the schedule and the loop at 1 U/h, IOB from the doses on
    /// a straight four-hour decline, and a target of 80 to 140. Each test bends one part of it.
    /// </summary>
    private sealed class Week
    {
        public long From { get; init; } = June;
        public int Days { get; init; } = 7;
        public long To => From + Days * Day;
        public Func<long, double> Glucose { get; set; } = _ => 120;
        public Func<long, bool> Reading { get; set; } = _ => true;
        public Func<long, double?> Iob { get; set; } = _ => null;
        public Func<long, double> Cob { get; set; } = _ => 0;
        public Func<long, bool> Tick { get; set; } = _ => true;
        public Func<long, IobCobSource> IobSource { get; set; } = _ => IobCobSource.Device;
        public Func<long, (double Rate, BasalDeliveryOrigin Origin)?> Basal { get; set; } = _ => (1, BasalDeliveryOrigin.Algorithm);
        public Func<long, (double Low, double High)?> Target { get; set; } = _ => (80, 140);
        public List<CarbEvent> Carbs { get; } = [];
        public List<InsulinDose> Doses { get; } = [];
        public LoopAnalysisMaskSources Sources { get; set; } = new();
        public LoopAnalysisMasks Masks { get; set; } = new();

        public long At(int day, int hour, int minute = 0) => From + day * Day + hour * Hour + minute * Minute;

        public SettingsEvidenceRange Run(bool listEpisodes = true)
        {
            var (start, end) = (From - Day, To + Day);
            var glucose = new List<GlucoseSample>();
            var ticks = new List<IobCobTick>();
            var delivered = new List<DeliveredBasalSegment>();
            for (var t = start; t < end; t += 5 * Minute)
            {
                if (Reading(t))
                    glucose.Add(new GlucoseSample(t, Glucose(t)));
                if (Tick(t))
                    ticks.Add(new IobCobTick(t, Iob(t) ?? DoseIob(t), IobSource(t), Cob(t), IobCobSource.Device));
                if (Basal(t) is { } basal)
                    delivered.Add(new DeliveredBasalSegment(t, t + 5 * Minute, basal.Rate, basal.Origin));
            }
            return Calculate(
                new DailyProfileInputs
                {
                    Zone = Sydney,
                    Glucose = glucose,
                    Insulin = new NetInsulinAboveSchedule(delivered, [new RateSegment(start, end, 1)], Doses),
                    Carbs = Carbs,
                    IobCob = ticks,
                    TargetAt = Target,
                },
                Therapy(start, end),
                From,
                To,
                Sources,
                Masks,
                listEpisodes);
        }

        private double DoseIob(long t) =>
            Doses.Where(d => d.Mills <= t).Sum(d => d.Units * Math.Max(0, 1 - (t - d.Mills) / (4.0 * Hour)));
    }

    /// <summary>
    /// A correction of 2 U at 14:00 every day: glucose at 200 for the hour before, falling evenly to
    /// 120 by 18:00. An ISF of 40.
    /// </summary>
    private static Week Corrections(int days = 7)
    {
        var week = new Week { Days = Math.Max(7, days) };
        var bolus = Enumerable.Range(0, days).Select(d => week.At(d, 14)).ToList();
        week.Doses.AddRange(bolus.Select(b => new InsulinDose(b, 2, Automatic: false)));
        week.Glucose = t =>
        {
            foreach (var b in bolus)
            {
                if (t >= b - Hour && t < b)
                    return 200;
                if (t >= b && t < b + 4 * Hour)
                    return 200 - 80.0 * (t - b) / (4 * Hour);
            }
            return 120;
        };
        return week;
    }

    /// <summary>40 g at 08:00 every day, bolused 4 U, glucose flat throughout: a carb ratio of 10.</summary>
    private static Week Meals(int days = 7)
    {
        var week = new Week { Days = Math.Max(7, days) };
        for (var d = 0; d < days; d++)
        {
            week.Carbs.Add(new CarbEvent(week.At(d, 8), 40));
            week.Doses.Add(new InsulinDose(week.At(d, 8), 4, Automatic: false));
        }
        return week;
    }

    private static int Rejected(SettingsEvidenceFigure figure, SettingsEvidenceRule rule) =>
        figure.Rejections.Single(r => r.Rule == rule).Count;

    private static MillsInterval[] Span(long start, long end) => [new(start, end)];

    private static readonly List<DayOfWeek> AllButThursday = [.. Enum.GetValues<DayOfWeek>().Where(d => d != DayOfWeek.Thursday)];

    [Fact]
    public void AFlatWeek_ReadsTheScheduleAsTheNeed_AndListsEveryRule()
    {
        var range = new Week().Run();

        range.Basal.Should().HaveCount(12);
        var block = BasalBlock(range, 2);
        block.N.Should().Be(7);
        block.Observed.Should().Be(1);
        block.Profile.Should().Be(1);
        block.MeanNetLoopCorrection.Should().Be(0);
        block.Consistency.Should().Be(1, "a correction of zero agrees with either sign");
        block.Confidence.Should().Be(SettingsEvidenceConfidence.Low);
        block.IsfDependent.Should().BeTrue();
        block.ConfidenceReasons.Should().Equal(
            SettingsEvidenceConfidenceReason.FewEpisodes,
            SettingsEvidenceConfidenceReason.FastingUnverifiable);
        block.Rejections.Select(r => r.Rule).Should().HaveCount(16).And.OnlyHaveUniqueItems();
        block.Rejections.Should().OnlyContain(r => r.Count == 0);
        var day = block.Days.First();
        day.Date.Should().Be("2024-06-03");
        day.Time.Should().Be("02:00");
        day.Hours.Should().Be(2);
    }

    public static TheoryData<SettingsEvidenceRule> BasalRules =>
    [
        SettingsEvidenceRule.OpenLoop, SettingsEvidenceRule.Override, SettingsEvidenceRule.TemporaryTarget,
        SettingsEvidenceRule.Exercise, SettingsEvidenceRule.Illness, SettingsEvidenceRule.CarbsBefore, SettingsEvidenceRule.RiseBefore,
        SettingsEvidenceRule.ManualBolus, SettingsEvidenceRule.CarbsOnBoard, SettingsEvidenceRule.Filtered,
        SettingsEvidenceRule.ManualDelivery, SettingsEvidenceRule.InferredDelivery, SettingsEvidenceRule.InsulinUncovered,
        SettingsEvidenceRule.IobUnknown, SettingsEvidenceRule.IobSourceChanged, SettingsEvidenceRule.MissingGlucose,
    ];

    [Theory]
    [MemberData(nameof(BasalRules))]
    public void EachBasalRule_RejectsTheBlockItFails_AndIsCounted(SettingsEvidenceRule rule)
    {
        var week = new Week();
        var (start, end) = (week.At(3, 2), week.At(3, 4));
        var inside = start + 30 * Minute;
        switch (rule)
        {
            case SettingsEvidenceRule.OpenLoop: week.Sources = new() { OpenLoop = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.Override: week.Sources = new() { Overrides = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.TemporaryTarget: week.Sources = new() { TemporaryTargets = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.Exercise: week.Sources = new() { Exercise = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.Illness: week.Sources = new() { Illness = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.CarbsBefore: week.Carbs.Add(new CarbEvent(start - 3 * Hour - 50 * Minute, 20)); break;
            case SettingsEvidenceRule.RiseBefore: week.Glucose = t => t >= start - Hour && t < start - 15 * Minute ? 120 + 80.0 * (t - start + Hour) / (45 * Minute) : t >= start - 15 * Minute && t < start + 30 * Minute ? 200 : 120; break;
            case SettingsEvidenceRule.ManualBolus: week.Doses.Add(new InsulinDose(start - 3 * Hour - 50 * Minute, 1, Automatic: false)); break;
            case SettingsEvidenceRule.CarbsOnBoard: week.Cob = t => t >= inside && t < inside + Hour ? 5 : 0; break;
            case SettingsEvidenceRule.Filtered: week.Masks = new() { Days = AllButThursday }; break;
            case SettingsEvidenceRule.ManualDelivery: week.Basal = t => (1.5, t >= inside && t < inside + Hour ? BasalDeliveryOrigin.Manual : BasalDeliveryOrigin.Algorithm); break;
            case SettingsEvidenceRule.InferredDelivery: week.Basal = t => t >= inside && t < inside + Hour ? (1.5, BasalDeliveryOrigin.Inferred) : (1, BasalDeliveryOrigin.Algorithm); break;
            case SettingsEvidenceRule.InsulinUncovered: week.Basal = t => t >= inside && t < inside + Hour ? null : (1, BasalDeliveryOrigin.Algorithm); break;
            case SettingsEvidenceRule.IobUnknown: week.Tick = t => t < start - 10 * Minute || t > start; break;
            case SettingsEvidenceRule.IobSourceChanged: week.IobSource = t => t == start ? IobCobSource.Calculated : IobCobSource.Device; break;
            case SettingsEvidenceRule.MissingGlucose: week.Reading = t => Math.Abs(t - end) > 5 * Minute; break;
        }

        var block = BasalBlock(week.Run(), 2);

        Rejected(block, rule).Should().Be(1, "June 6th is a Thursday");
        block.N.Should().Be(6);
        block.Rejections.Sum(r => r.Count).Should().Be(1);
        block.Days.Select(d => d.Date).Should().NotContain("2024-06-06");
    }

    [Fact]
    public void TheCarbsAndBolusLookback_ReachFourAndFiveHoursBeforeTheBlock_NotTheDia()
    {
        var week = new Week();
        var block = week.At(3, 2);
        week.Carbs.Add(new CarbEvent(block - 4 * Hour - Minute, 20));
        week.Doses.Add(new InsulinDose(block - 5 * Hour - Minute, 1, Automatic: false));

        var range = week.Run();

        Rejected(BasalBlock(range, 2), SettingsEvidenceRule.CarbsBefore).Should().Be(0);
        Rejected(BasalBlock(range, 2), SettingsEvidenceRule.ManualBolus).Should().Be(0);
        Rejected(BasalBlock(range, 0), SettingsEvidenceRule.CarbsBefore).Should().Be(1);
        Rejected(BasalBlock(range, 0), SettingsEvidenceRule.ManualBolus).Should().Be(0, "the carbs before it are counted first");
    }

    [Fact]
    public void AnEveningBolus_DoesNotRejectTheNightsBlocks_UnderALongDia()
    {
        var week = new Week();
        week.Doses.Add(new InsulinDose(week.At(2, 20), 1, Automatic: false));

        var range = week.Run();

        Rejected(BasalBlock(range, 0), SettingsEvidenceRule.ManualBolus).Should().Be(1, "20:00 is within five hours of 00:00");
        Rejected(BasalBlock(range, 2), SettingsEvidenceRule.ManualBolus).Should().Be(0, "a ten-hour DIA would reach it; the lookback does not");
    }

    [Fact]
    public void FewerFastingDaysThanTheMinimum_LeaveTheNeedUnavailable_ButReportTheDays()
    {
        var block = BasalBlock(new Week { Days = ObservedBasalNeed.MinimumDays - 1 }.Run(), 2);

        block.UnavailableReason.Should().Be(SettingsEvidenceUnavailableReason.TooFew);
        block.Observed.Should().BeNull();
        block.Spread.Should().BeNull();
        block.Confidence.Should().BeNull();
        block.N.Should().Be(ObservedBasalNeed.MinimumDays - 1);
        block.Days.Should().HaveCount(ObservedBasalNeed.MinimumDays - 1);
    }

    [Fact]
    public void ASteepTrend_SuppressesTheNeed()
    {
        var week = new Week();
        week.Glucose = t =>
        {
            var hour = LocalSlotBinning.LocalTime(t, Sydney).TimeOfDay.TotalHours;
            return hour is >= 2 and <= 4 ? 120 + 15 * (hour - 2) : 120;
        };

        var block = BasalBlock(week.Run(), 2);

        block.GlucoseTrend!.P50.Should().Be(15);
        block.UnavailableReason.Should().Be(SettingsEvidenceUnavailableReason.SteepTrend);
        block.Observed.Should().BeNull();
    }

    [Fact]
    public void ACorrectionThatChangesSignFromDayToDay_IsInconsistent()
    {
        var week = new Week();
        week.Basal = t => (LocalSlotBinning.LocalDay(t, Sydney).Day % 2 == 0 ? 1.3 : 0.7, BasalDeliveryOrigin.Algorithm);

        var block = BasalBlock(week.Run(), 2);

        block.Consistency.Should().BeApproximately(4.0 / 7, 0.001);
        block.UnavailableReason.Should().Be(SettingsEvidenceUnavailableReason.Inconsistent);
        block.MeanNetLoopCorrection.Should().BeApproximately(-0.3 / 7, 0.001, "four of the seven days are odd");
    }

    [Fact]
    public void InsulinCarriedIntoAFastingBlock_CountsAsActingInIt()
    {
        var week = new Week();
        // IOB falls from 0.6 U to 0.2 U across 02:00-04:00 every day, with glucose flat: 0.2 U/h of
        // earlier insulin met the block's need, on top of the schedule.
        week.Iob = t =>
        {
            var hour = LocalSlotBinning.LocalTime(t, Sydney).TimeOfDay.TotalHours;
            return hour is >= 1 and <= 5 ? 0.6 - 0.2 * (hour - 2) : 0;
        };

        var block = BasalBlock(week.Run(), 2);

        block.Days.Should().OnlyContain(d => d.CarriedInsulin == 0.2);
        block.Observed.Should().BeApproximately(1.2, 0.001);
    }

    [Fact]
    public void ACorrection_ReadsItsDropOverTheInsulinThatActed()
    {
        var range = Corrections().Run();

        var midday = Isf(range, SettingsEvidenceBlock.Midday);
        midday.N.Should().Be(7);
        midday.Observed.Should().Be(40);
        midday.Profile.Should().Be(40);
        midday.Confidence.Should().Be(SettingsEvidenceConfidence.Low);
        midday.ConfidenceReasons.Should().Equal(SettingsEvidenceConfidenceReason.FewEpisodes, SettingsEvidenceConfidenceReason.BasalDependent);
        midday.BasalDependent.Should().BeTrue();
        var episode = midday.Episodes.First();
        episode.Time.Should().Be("14:00");
        episode.Units.Should().Be(2);
        episode.Drop.Should().Be(80);
        episode.IobAtStart.Should().Be(2, "the tick at the bolus holds it");
        episode.InsulinActed.Should().Be(2);
        (episode.End - episode.At).Should().Be(TimeSpan.FromHours(4));
        Isf(range, SettingsEvidenceBlock.Morning).UnavailableReason.Should().Be(SettingsEvidenceUnavailableReason.NoCandidates);
    }

    [Fact]
    public void IobStillOnBoardAtTheWindowsEnd_IsTakenOffTheInsulinThatActed()
    {
        var week = Corrections();
        var bolus = week.At(2, 14);
        week.Iob = t => t >= bolus && t < bolus + 4 * Hour + 5 * Minute
            ? 2 - 1.5 * Math.Min(1, (t - bolus) / (4.0 * Hour))
            : null;

        var episode = Isf(week.Run(), SettingsEvidenceBlock.Midday).Episodes.Single(e => e.Date == "2024-06-05");

        episode.IobAtEnd.Should().Be(0.5);
        episode.NetInsulin.Should().Be(0, "the bolus is in the IOB at the window's start, not delivered after it");
        episode.InsulinActed.Should().Be(1.5);
        episode.Value.Should().BeApproximately(80 / 1.5, 0.001);
    }

    public static TheoryData<SettingsEvidenceRule> IsfRules =>
    [
        SettingsEvidenceRule.CarbsNear, SettingsEvidenceRule.NotIsolated, SettingsEvidenceRule.Override, SettingsEvidenceRule.TemporaryTarget,
        SettingsEvidenceRule.Exercise, SettingsEvidenceRule.Illness, SettingsEvidenceRule.Filtered,
        SettingsEvidenceRule.MissingGlucose, SettingsEvidenceRule.NoTarget, SettingsEvidenceRule.NotAboveTarget, SettingsEvidenceRule.StartUnstable,
        SettingsEvidenceRule.IobUnknown, SettingsEvidenceRule.IobAtBolus, SettingsEvidenceRule.IobSourceChanged,
        SettingsEvidenceRule.InsulinUncovered, SettingsEvidenceRule.WentLow, SettingsEvidenceRule.TooLittleInsulin,
    ];

    [Theory]
    [MemberData(nameof(IsfRules))]
    public void EachIsfRule_RejectsTheCorrectionItFails_AndIsCounted(SettingsEvidenceRule rule)
    {
        var week = Corrections();
        var bolus = week.At(3, 14);
        var end = bolus + 4 * Hour;
        var inside = bolus + Hour;
        var target = week.Target;
        switch (rule)
        {
            case SettingsEvidenceRule.CarbsNear: week.Carbs.Add(new CarbEvent(bolus + 2 * Hour, 15)); break;
            case SettingsEvidenceRule.NotIsolated: week.Doses.Add(new InsulinDose(bolus + 3 * Hour + 30 * Minute, 1, Automatic: false)); break;
            case SettingsEvidenceRule.Override: week.Sources = new() { Overrides = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.TemporaryTarget: week.Sources = new() { TemporaryTargets = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.Exercise: week.Sources = new() { Exercise = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.Illness: week.Sources = new() { Illness = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.Filtered: week.Masks = new() { Days = AllButThursday }; break;
            case SettingsEvidenceRule.MissingGlucose: week.Reading = t => Math.Abs(t - end) > 5 * Minute; break;
            case SettingsEvidenceRule.NoTarget: week.Target = t => t == bolus ? null : target(t); break;
            case SettingsEvidenceRule.NotAboveTarget: week.Target = t => t == bolus ? (80, 200) : target(t); break;
            case SettingsEvidenceRule.StartUnstable: { var glucose = week.Glucose; week.Glucose = t => Math.Abs(t - (bolus - 15 * Minute)) <= 5 * Minute ? 170 : glucose(t); break; }
            case SettingsEvidenceRule.IobUnknown: week.Tick = t => t != bolus; break;
            case SettingsEvidenceRule.IobAtBolus: week.Iob = t => t == bolus - 5 * Minute ? 2.1 : null; break;
            case SettingsEvidenceRule.InsulinUncovered: week.Basal = t => t >= inside && t < inside + Hour ? null : (1, BasalDeliveryOrigin.Algorithm); break;
            case SettingsEvidenceRule.IobSourceChanged: week.IobSource = t => t == end ? IobCobSource.Calculated : IobCobSource.Device; break;
            case SettingsEvidenceRule.WentLow: { var glucose = week.Glucose; week.Glucose = t => Math.Abs(t - (bolus + 2 * Hour)) <= 5 * Minute ? 65 : glucose(t); break; }
            case SettingsEvidenceRule.TooLittleInsulin: week.Iob = t => t == end ? 1.6 : null; break;
        }

        var midday = Isf(week.Run(), SettingsEvidenceBlock.Midday);

        Rejected(midday, rule).Should().Be(1);
        midday.Rejections.Sum(r => r.Count).Should().Be(1);
        midday.N.Should().Be(6);
    }

    [Fact]
    public void CarbsLateInTheWindow_EndItAtThemInsteadOfRejectingIt()
    {
        var week = Corrections();
        var bolus = week.At(3, 14);
        week.Carbs.Add(new CarbEvent(bolus + 3 * Hour + 30 * Minute, 30));

        var midday = Isf(week.Run(), SettingsEvidenceBlock.Midday);

        midday.N.Should().Be(7);
        var episode = midday.Episodes.Single(e => e.Date == "2024-06-06");
        (episode.End - episode.At).Should().Be(TimeSpan.FromMinutes(210));
        episode.EndGlucose.Should().Be(130);
    }

    [Fact]
    public void FewerCorrectionsThanTheMinimum_LeaveTheIsfUnavailable()
    {
        var midday = Isf(Corrections(days: SettingsEvidenceCalculator.MinimumEpisodes - 1).Run(), SettingsEvidenceBlock.Midday);

        midday.UnavailableReason.Should().Be(SettingsEvidenceUnavailableReason.TooFew);
        midday.Observed.Should().BeNull();
        midday.Episodes.Should().HaveCount(SettingsEvidenceCalculator.MinimumEpisodes - 1);
    }

    [Fact]
    public void AMeal_ReadsItsGramsOverTheInsulinItNeeded()
    {
        var range = Meals().Run();

        var morning = CarbRatio(range, SettingsEvidenceBlock.Morning);
        morning.N.Should().Be(7);
        morning.Observed.Should().Be(10);
        morning.Profile.Should().Be(10);
        morning.IsfDependent.Should().BeTrue("no correction gave an observed ISF");
        morning.BasalDependent.Should().BeTrue("a week of flat data gives no basal need confident enough to read above");
        var episode = morning.Episodes.First();
        episode.Time.Should().Be("08:00");
        episode.Grams.Should().Be(40);
        episode.InsulinActed.Should().Be(4);
        episode.InsulinNeeded.Should().Be(4);
        (episode.End - episode.At).Should().Be(TimeSpan.FromHours(4));
    }

    public static TheoryData<SettingsEvidenceRule> CarbRatioRules =>
    [
        SettingsEvidenceRule.NotIsolated, SettingsEvidenceRule.Override, SettingsEvidenceRule.TemporaryTarget,
        SettingsEvidenceRule.Exercise, SettingsEvidenceRule.Illness, SettingsEvidenceRule.Filtered,
        SettingsEvidenceRule.MissingGlucose, SettingsEvidenceRule.StartOutOfRange, SettingsEvidenceRule.StartUnstable,
        SettingsEvidenceRule.IobUnknown, SettingsEvidenceRule.IobSourceChanged, SettingsEvidenceRule.InsulinUncovered,
        SettingsEvidenceRule.TooLittleInsulin,
    ];

    [Theory]
    [MemberData(nameof(CarbRatioRules))]
    public void EachCarbRatioRule_RejectsTheMealItFails_AndIsCounted(SettingsEvidenceRule rule)
    {
        var week = Meals();
        var meal = week.At(3, 8);
        var start = meal - 15 * Minute;
        var inside = meal + Hour;
        switch (rule)
        {
            case SettingsEvidenceRule.NotIsolated: week.Doses.Add(new InsulinDose(meal + 2 * Hour, 1, Automatic: false)); break;
            case SettingsEvidenceRule.Override: week.Sources = new() { Overrides = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.TemporaryTarget: week.Sources = new() { TemporaryTargets = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.Exercise: week.Sources = new() { Exercise = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.Illness: week.Sources = new() { Illness = Span(inside, inside + Hour) }; break;
            case SettingsEvidenceRule.Filtered: week.Masks = new() { Days = AllButThursday }; break;
            case SettingsEvidenceRule.MissingGlucose: week.Reading = t => Math.Abs(t - start) > 5 * Minute; break;
            case SettingsEvidenceRule.StartOutOfRange: week.Glucose = t => Math.Abs(t - start) <= 5 * Minute ? 190 : 120; break;
            case SettingsEvidenceRule.StartUnstable: week.Glucose = t => Math.Abs(t - start) <= 5 * Minute ? 100 : 120; break;
            case SettingsEvidenceRule.IobUnknown: week.Tick = t => t != start; break;
            case SettingsEvidenceRule.IobSourceChanged: week.IobSource = t => t == start ? IobCobSource.Calculated : IobCobSource.Device; break;
            case SettingsEvidenceRule.InsulinUncovered: week.Basal = t => t >= inside && t < inside + Hour ? null : (1, BasalDeliveryOrigin.Algorithm); break;
            case SettingsEvidenceRule.TooLittleInsulin: week.Doses.RemoveAll(d => d.Mills == meal); break;
        }

        var morning = CarbRatio(week.Run(), SettingsEvidenceBlock.Morning);

        Rejected(morning, rule).Should().Be(1);
        morning.Rejections.Sum(r => r.Count).Should().Be(1);
        morning.N.Should().Be(6);
    }

    [Fact]
    public void WithNoAnnouncedMeals_TheCarbRatioIsUnavailable_AndSaysWhy()
    {
        var range = Corrections().Run();

        range.CarbRatio.Should().HaveCount(4);
        range.CarbRatio.Should().OnlyContain(b =>
            b.UnavailableReason == SettingsEvidenceUnavailableReason.NoAnnouncedMeals
            && b.Observed == null && b.Spread == null && b.Confidence == null
            && b.Episodes.Count == 0 && !b.IsfDependent);
    }

    [Fact]
    public void MealsThatAreNeverIsolated_LeaveTheCarbRatioUnavailableForThat()
    {
        var week = Meals();
        week.Doses.AddRange(Enumerable.Range(0, 7).Select(d => new InsulinDose(week.At(d, 10), 1, Automatic: false)));

        var morning = CarbRatio(week.Run(), SettingsEvidenceBlock.Morning);

        morning.UnavailableReason.Should().Be(SettingsEvidenceUnavailableReason.NoneIsolated);
        Rejected(morning, SettingsEvidenceRule.NotIsolated).Should().Be(7);
    }

    [Fact]
    public void AnUnannouncedRiseInTheWindow_LeavesTheMealNotIsolated()
    {
        var week = Meals(days: 1);
        var rise = week.At(0, 10);
        week.Glucose = t => t < rise ? 120 : t < rise + 45 * Minute ? 120 + 80.0 * (t - rise) / (45 * Minute) : 200;

        var morning = CarbRatio(week.Run(), SettingsEvidenceBlock.Morning);

        Rejected(morning, SettingsEvidenceRule.NotIsolated).Should().Be(1);
    }

    [Fact]
    public void TheComparisonRange_ListsNoEpisodes()
    {
        var range = Corrections().Run(listEpisodes: false);

        range.Basal.Should().OnlyContain(b => b.Days.Count == 0);
        var midday = Isf(range, SettingsEvidenceBlock.Midday);
        midday.Episodes.Should().BeEmpty();
        midday.N.Should().Be(7);
        midday.Observed.Should().Be(40);
    }

    [Fact]
    public void BlocksAcrossDstChanges_RunAnHourLongOrShort_AndStayInTheirLocalBlock()
    {
        // 2024-04-07 in Sydney: 03:00 AEDT falls back to 02:00 AEST, so 02:00-04:00 holds three hours.
        // 2024-10-06: 02:00 AEST springs forward to 03:00 AEDT, so it holds one.
        var fallBack = new Week { From = Local(2024, 4, 4, 0) };
        var springForward = new Week { From = Local(2024, 10, 3, 0) };

        var autumn = fallBack.Run();
        var spring = springForward.Run();

        BasalBlock(autumn, 2).Days.Single(d => d.Date == "2024-04-07").Hours.Should().Be(3);
        BasalBlock(spring, 2).Days.Single(d => d.Date == "2024-10-06").Hours.Should().Be(1);
        BasalBlock(spring, 2).Days.Single(d => d.Date == "2024-10-06").Time.Should().Be("03:00");
        BasalBlock(autumn, 4).Days.Single(d => d.Date == "2024-04-07").Time.Should().Be("04:00");
        foreach (var block in new[] { BasalBlock(autumn, 2), BasalBlock(spring, 2) })
        {
            block.N.Should().Be(7);
            block.Observed.Should().Be(1, "a rate is per hour however long the block ran");
        }
    }

    [Fact]
    public void ACorrectionOnTheDstMorning_FallsInTheBlockOfItsLocalHour()
    {
        var week = new Week { From = Local(2024, 10, 3, 0) };
        var bolus = Local(2024, 10, 6, 5, 30);
        week.Doses.Add(new InsulinDose(bolus, 2, Automatic: false));
        week.Glucose = t => t >= bolus - Hour && t < bolus ? 200
            : t >= bolus && t < bolus + 4 * Hour ? 200 - 80.0 * (t - bolus) / (4 * Hour)
            : 120;

        var range = week.Run();

        Isf(range, SettingsEvidenceBlock.Overnight).Episodes.Single().Time.Should().Be("05:30");
        Isf(range, SettingsEvidenceBlock.Morning).UnavailableReason.Should().Be(SettingsEvidenceUnavailableReason.NoCandidates);
    }

    [Fact]
    public void AClimbPeakingWithinFourHoursOfTheBlock_RejectsIt_ThoughItStartedEarlier()
    {
        var week = new Week();
        var climb = week.At(3, 2) - 5 * Hour;
        week.Glucose = t =>
            t < climb || t >= climb + 3 * Hour ? 120
            : t < climb + 45 * Minute ? 120 + 80.0 * (t - climb) / (45 * Minute)
            : t < climb + 90 * Minute ? 200 + 30.0 * (t - climb - 45 * Minute) / (45 * Minute)
            : 230 - 110.0 * (t - climb - 90 * Minute) / (90 * Minute);

        var block = BasalBlock(week.Run(), 2);

        Rejected(block, SettingsEvidenceRule.RiseBefore).Should().Be(1, "it started five hours before 02:00 but peaked three and a half before");
    }

    [Fact]
    public void FastingIsVerifiable_OnlyWhereAFifthOfTheDaysHoldACarbEntry()
    {
        var week = new Week();
        week.Carbs.Add(new CarbEvent(week.At(3, 12), 30));
        BasalBlock(week.Run(), 2).ConfidenceReasons.Should().Contain(
            SettingsEvidenceConfidenceReason.FastingUnverifiable, "one snack in a week is the exception");

        week.Carbs.Add(new CarbEvent(week.At(5, 12), 30));
        BasalBlock(week.Run(), 2).ConfidenceReasons.Should().NotContain(SettingsEvidenceConfidenceReason.FastingUnverifiable);
    }

    [Fact]
    public void ACorrectionEndingBelowTarget_StillCounts_ButOneThatWentLowDoesNot()
    {
        var week = Corrections();
        var target = week.Target;
        week.Target = t => LocalSlotBinning.LocalTime(t, Sydney).Hour == 18 ? (125, 140) : target(t);

        var midday = Isf(week.Run(), SettingsEvidenceBlock.Midday);

        midday.N.Should().Be(7, "ending at 120 under a 125 target is a drop like any other");
        Rejected(midday, SettingsEvidenceRule.WentLow).Should().Be(0);
    }

    [Fact]
    public void MuchIobLeftAtTheWindowsEnd_LowersConfidence()
    {
        var week = Corrections();
        week.Iob = t => LocalSlotBinning.LocalTime(t, Sydney) is { Hour: 18, Minute: 0 } ? 0.9 : null;

        var midday = Isf(week.Run(), SettingsEvidenceBlock.Midday);

        midday.Episodes.Should().OnlyContain(e => e.InsulinActed == 1.1);
        midday.ConfidenceReasons.Should().Contain(SettingsEvidenceConfidenceReason.ResidualIob, "0.9 of 2 U is still on board");
    }

    [Fact]
    public void AStandingLoopTopUp_IsNotResidualIob()
    {
        var week = Corrections();
        var boluses = week.Doses.Select(d => d.Mills).ToList();
        week.Iob = t => 0.8 + boluses.Where(b => b <= t).Sum(b => 2 * Math.Max(0, 1 - (t - b) / (4.0 * Hour)));

        var midday = Isf(week.Run(), SettingsEvidenceBlock.Midday);

        midday.Episodes.Should().OnlyContain(e => e.IobAtEnd == 0.8 && e.ResidualIob == 0);
        midday.ConfidenceReasons.Should().NotContain(SettingsEvidenceConfidenceReason.ResidualIob);
    }

    [Fact]
    public void MostMealsNotIsolated_LowersConfidenceInTheFew_ThatWere()
    {
        var week = Meals(days: 14);
        week.Doses.AddRange(Enumerable.Range(0, 8).Select(d => new InsulinDose(week.At(d, 10), 1, Automatic: false)));

        var morning = CarbRatio(week.Run(), SettingsEvidenceBlock.Morning);

        morning.N.Should().Be(6);
        Rejected(morning, SettingsEvidenceRule.NotIsolated).Should().Be(8);
        morning.ConfidenceReasons.Should().Contain(SettingsEvidenceConfidenceReason.MostNotIsolated);
    }

    [Fact]
    public void AWindowReachingPastTheRange_IsOutsideIt_NotFiltered()
    {
        var week = Corrections();
        week.Doses.Add(new InsulinDose(week.At(6, 22), 2, Automatic: false));
        week.Carbs.Add(new CarbEvent(week.At(0, 0, 10), 30));
        week.Doses.Add(new InsulinDose(week.At(0, 0, 10), 3, Automatic: false));

        var range = week.Run();

        Rejected(Isf(range, SettingsEvidenceBlock.Evening), SettingsEvidenceRule.OutsideRange).Should().Be(1);
        Rejected(CarbRatio(range, SettingsEvidenceBlock.Overnight), SettingsEvidenceRule.OutsideRange).Should().Be(1);
    }

    private static SettingsEvidenceFigure Rated(int n, double observed, double profile, bool higherMeansMoreInsulin, bool borrowed,
        params SettingsEvidenceConfidenceReason[] reasons)
    {
        var figure = new SettingsEvidenceFigure { Profile = profile, N = n };
        SettingsEvidenceCalculator.Rate(
            figure, observed, [.. Enumerable.Repeat(observed, n)], higherMeansMoreInsulin, borrowed, reasons);
        return figure;
    }

    [Fact]
    public void FewerThanSixEpisodes_AreLowConfidence_AndOnlyAWellFoundedFigureIsLent()
    {
        Rated(5, 10, 10, false, false).Confidence.Should().Be(SettingsEvidenceConfidence.Low);

        var few = Rated(8, 10, 10, false, false);
        few.Confidence.Should().Be(SettingsEvidenceConfidence.Moderate);
        SettingsEvidenceCalculator.Lendable(few).Should().BeFalse("its one reason is its n");

        var dependent = Rated(12, 10, 10, false, false, SettingsEvidenceConfidenceReason.BasalDependent);
        dependent.Confidence.Should().Be(SettingsEvidenceConfidence.Moderate);
        SettingsEvidenceCalculator.Lendable(dependent).Should().BeTrue();

        SettingsEvidenceCalculator.Lendable(Rated(12, 10, 10, false, false)).Should().BeTrue();
    }

    [Fact]
    public void AFigureTowardsMoreInsulin_IsLoweredALevel_OnlyWhereABorrowedInputIsMaterial()
    {
        var capped = Rated(12, 8, 10, higherMeansMoreInsulin: false, borrowed: true);
        capped.Confidence.Should().Be(SettingsEvidenceConfidence.Moderate);
        capped.ConfidenceReasons.Should().Equal(SettingsEvidenceConfidenceReason.MoreInsulinOnBorrowedInput);

        Rated(12, 8, 10, higherMeansMoreInsulin: false, borrowed: false).Confidence.Should().Be(SettingsEvidenceConfidence.High);
        Rated(12, 12, 10, higherMeansMoreInsulin: false, borrowed: true).Confidence.Should().Be(SettingsEvidenceConfidence.High);
        Rated(12, 1.2, 1.0, higherMeansMoreInsulin: true, borrowed: true).Confidence.Should().Be(SettingsEvidenceConfidence.Moderate);
        Rated(12, 8, 10, higherMeansMoreInsulin: false, borrowed: true, SettingsEvidenceConfidenceReason.BasalDependent)
            .Confidence.Should().Be(SettingsEvidenceConfidence.Low, "it is lowered a level from Moderate, not held there");
    }

    #endregion
}
