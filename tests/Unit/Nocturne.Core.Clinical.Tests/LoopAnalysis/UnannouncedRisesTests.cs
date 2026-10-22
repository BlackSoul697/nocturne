using Nocturne.Core.Clinical.LoopAnalysis;
using Nocturne.Core.Models.LoopAnalysis;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Clinical.Tests.LoopAnalysis;

public class UnannouncedRisesTests
{
    private const long Minute = 60_000;
    private static readonly long T0 = new DateTimeOffset(2024, 6, 10, 2, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>Readings every five minutes, valued by minutes since <see cref="T0"/>.</summary>
    private static List<GlucoseSample> Trace(long fromMinute, long toMinute, Func<double, double> mgdl)
    {
        var readings = new List<GlucoseSample>();
        for (var m = fromMinute; m < toMinute; m += 5)
            readings.Add(new GlucoseSample(T0 + m * Minute, mgdl(m)));
        return readings;
    }

    /// <summary>Flat at 110 until T0, then climbing <paramref name="rate"/> mg/dL a minute to 200.</summary>
    private static List<GlucoseSample> Meal(double rate = 3) =>
        Trace(-90, 240, m => m <= 0 ? 110 : Math.Min(200, 110 + rate * m));

    /// <summary>A 1 U/h schedule, delivered as scheduled except where <paramref name="temps"/> run.</summary>
    private static NetInsulinAboveSchedule Insulin(
        IEnumerable<InsulinDose>? doses = null, IEnumerable<DeliveredBasalSegment>? temps = null)
    {
        var from = T0 - 3 * 60 * Minute;
        var to = T0 + 6 * 60 * Minute;
        return new NetInsulinAboveSchedule(
            temps ?? [new(from, to, 1.0, BasalDeliveryOrigin.Algorithm)], [new(from, to, 1.0)], doses ?? []);
    }

    private static List<EventAnchor> Find(
        List<GlucoseSample> glucose,
        IntervalSet? exclusions = null,
        IReadOnlyList<CarbEvent>? carbs = null,
        NetInsulinAboveSchedule? insulin = null,
        bool requireCorroboration = false) =>
        UnannouncedRises.Find(glucose, exclusions ?? IntervalSet.Empty, carbs ?? [], insulin ?? Insulin(), requireCorroboration);

    private static long At(long minute) => T0 + minute * Minute;

    [Fact]
    public void AnUnannouncedMeal_IsAnchoredWhereTheClimbLeavesTheFloor()
    {
        Find(Meal()).Should().ContainSingle().Which.Should().Be(new EventAnchor(T0, EventAnchorKind.UnannouncedRise));
    }

    [Fact]
    public void ADipBeforeTheClimb_DoesNotMoveTheAnchorBackToIt()
    {
        var glucose = Meal(2).Select(g => g.Mills == At(-25) ? g with { Mgdl = 104 } : g).ToList();

        Find(glucose).Should().ContainSingle().Which.Mills.Should().Be(T0);
    }

    [Fact]
    public void AlternatingNoise_StillFindsTheMealAtItsStart()
    {
        var glucose = Trace(-90, 240, m =>
            (m <= 0 ? 110 : Math.Min(200, 110 + 2 * m)) + ((m / 5) % 2 == 0 ? 3 : -3));

        Find(glucose).Should().ContainSingle().Which.Mills.Should().BeInRange(At(-5), T0);
    }

    [Fact]
    public void ASlowMeal_AtOneMgdlAMinute_IsFound()
    {
        Find(Meal(1)).Should().ContainSingle().Which.Mills
            .Should().BeInRange(T0, At(5), "at 1 mg/dL a minute the first reading of the climb is still within the floor's tolerance");
    }

    [Fact]
    public void ARiseSlowerThanTheWindow_IsNotAMeal()
    {
        Find(Meal(0.7)).Should().BeEmpty();
    }

    [Fact]
    public void ARiseAnsweredByAutomaticBolusesLate_IsCorroborated()
    {
        var smbs = new[] { 15, 20, 25, 30 }.Select(m => new InsulinDose(At(m), 0.2, true)).ToList();

        Find(Meal(), insulin: Insulin(smbs), requireCorroboration: true).Should().ContainSingle();
    }

    [Fact]
    public void ARiseAnsweredByTempBasalsAlone_IsCorroborated()
    {
        var temps = new List<DeliveredBasalSegment>
        {
            new(At(-180), At(5), 1.0, BasalDeliveryOrigin.Algorithm),
            new(At(5), At(50), 2.5, BasalDeliveryOrigin.Algorithm),
            new(At(50), At(360), 1.0, BasalDeliveryOrigin.Algorithm),
        };

        Find(Meal(), insulin: Insulin(temps: temps), requireCorroboration: true)
            .Should().ContainSingle("1.5 U/h above a 1 U/h schedule for 40 minutes is a full hour's half");
    }

    [Fact]
    public void ASteepDawnRise_FailsCorroboration()
    {
        // At the steep end of the dawn phenomenon, 1 mg/dL a minute, glucose alone reads it as a
        // rise; the loop answers it with a trickle well short of half an hour's schedule.
        var dawn = Trace(-60, 180, m => m <= 0 ? 120 : Math.Min(180, 120 + m));
        var trickle = Enumerable.Range(0, 6).Select(i => new InsulinDose(At(i * 5), 0.05, true)).ToList();

        Find(dawn, insulin: Insulin(trickle)).Should().ContainSingle();
        Find(dawn, insulin: Insulin(trickle), requireCorroboration: true).Should().BeEmpty();
    }

    [Fact]
    public void ACompressionLowRecovery_NeverQualifies()
    {
        // Steady at 120, a false low at 60 for 35 minutes, then straight back to 120.
        var glucose = Trace(-60, 120, m => m is >= 0 and < 35 ? 60 : 120);

        Find(glucose, exclusions: IntervalSet.Of(T0, At(35))).Should().BeEmpty("the low is excluded, so the climb has no stable start");
        Find(glucose).Should().BeEmpty("unexcluded, its floor is below 70 and its climb faster than glucose moves");
    }

    [Fact]
    public void AClimbFasterThanGlucoseMoves_IsAnArtefact()
    {
        var glucose = Trace(-60, 120, m => m <= 0 ? 110 : m < 10 ? 110 + 7 * m : 180);

        Find(glucose).Should().BeEmpty();
    }

    [Fact]
    public void AHypoRebound_IsNotAMeal()
    {
        Find(Trace(-60, 120, m => m <= 0 ? 62 : Math.Min(150, 62 + 2 * m))).Should().BeEmpty();
    }

    [Fact]
    public void AMealThatStallsAndClimbsAgain_IsOneRise()
    {
        var glucose = Trace(-60, 300, m => m switch
        {
            <= 0 => 110,
            <= 20 => 110 + 3 * m,
            <= 70 => 170,
            _ => Math.Min(240, 170 + 3 * (m - 70)),
        });

        Find(glucose).Should().ContainSingle().Which.Mills.Should().Be(T0);
    }

    [Fact]
    public void AFloorSetComingDownFromAPeak_DoesNotStandHoursLater()
    {
        // Coming down from 190 to 110 over two hours, resting at 110 for four, then a meal. The
        // stretch near the end of the fall reads a floor near 128; a reading of 125 four hours later
        // must not count as resting at it.
        var glucose = Trace(-360, 120, m => m switch
        {
            < -240 => 190 - 80 * (m + 360) / 120.0,
            <= 0 => 110,
            _ => Math.Min(200, 110 + 3 * m),
        });

        Find(glucose).Should().ContainSingle(a => a.Mills > At(-60)).Which.Mills.Should().Be(T0);
    }

    [Fact]
    public void ARise_WithCarbsTwentyMinutesLater_IsNotAnchored()
    {
        Find(Meal(), carbs: [new CarbEvent(At(20), 45)]).Should().BeEmpty();
        Find(Meal(), carbs: [new CarbEvent(At(31), 45)])
            .Should().ContainSingle("a carb entry over half an hour away does not announce it");
    }

    [Fact]
    public void ARiseFromAFall_HasNoStableStart()
    {
        Find(Trace(-60, 120, m => m <= 0 ? 110 - m : 110 + 3 * m)).Should().BeEmpty();
    }

    [Fact]
    public void ARiseAcrossAGap_IsNotReadAcrossIt()
    {
        Find(Meal().Where(g => g.Mills <= T0 || g.Mills > At(20)).ToList()).Should().BeEmpty();
    }
}
