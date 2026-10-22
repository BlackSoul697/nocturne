using Nocturne.Core.Clinical.LoopAnalysis;
using Nocturne.Core.Models.LoopAnalysis;

namespace Nocturne.Core.Clinical.Tests.LoopAnalysis;

public class UnannouncedRisesTests
{
    private const long Minute = 60_000;
    private static readonly long T0 = new DateTimeOffset(2024, 6, 10, 2, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>Readings every five minutes from <paramref name="from"/>, valued by minutes since <see cref="T0"/>.</summary>
    private static List<GlucoseSample> Trace(long from, long to, Func<double, double> mgdl)
    {
        var readings = new List<GlucoseSample>();
        for (var t = from; t < to; t += 5 * Minute)
            readings.Add(new GlucoseSample(t, mgdl((t - T0) / (double)Minute)));
        return readings;
    }

    /// <summary>Flat at 110 until T0, then climbing 3 mg/dL a minute to 200 and holding.</summary>
    private static List<GlucoseSample> UnannouncedMeal() =>
        Trace(T0 - 90 * Minute, T0 + 180 * Minute, m => m <= 0 ? 110 : Math.Min(200, 110 + 3 * m));

    private static List<EventAnchor> Find(
        List<GlucoseSample> glucose,
        IntervalSet? exclusions = null,
        IReadOnlyList<CarbEvent>? carbs = null,
        IReadOnlyList<InsulinDose>? automatic = null,
        bool requireCluster = false) =>
        UnannouncedRises.Find(glucose, exclusions ?? IntervalSet.Empty, carbs ?? [], automatic ?? [], requireCluster);

    [Fact]
    public void AnUnannouncedMeal_IsAnchoredAtTheStartOfItsRise()
    {
        var anchors = Find(UnannouncedMeal());

        anchors.Should().ContainSingle().Which.Should().Be(new EventAnchor(T0, EventAnchorKind.UnannouncedRise));
    }

    [Fact]
    public void AnUnannouncedMeal_AnsweredByAutomaticBoluses_PassesCorroboration()
    {
        var smbs = new List<InsulinDose> { new(T0 + 10 * Minute, 0.3, true), new(T0 + 15 * Minute, 0.4, true) };

        Find(UnannouncedMeal(), automatic: smbs, requireCluster: true).Should().ContainSingle();
    }

    [Fact]
    public void ADawnRise_FailsCorroboration()
    {
        // Climbs 1 mg/dL a minute: fast enough to qualify on glucose alone, but the loop answers
        // it with a trickle of tenths.
        var dawn = Trace(T0 - 60 * Minute, T0 + 120 * Minute, m => m <= 0 ? 120 : Math.Min(180, 120 + m));
        var trickle = Enumerable.Range(0, 6).Select(i => new InsulinDose(T0 + i * 5 * Minute, 0.05, true)).ToList();

        Find(dawn, automatic: trickle).Should().ContainSingle("glucose alone reads it as a rise");
        Find(dawn, automatic: trickle, requireCluster: true).Should().BeEmpty();
    }

    [Fact]
    public void ACompressionLowRecovery_UnderADataExclusion_NeverQualifies()
    {
        // Steady at 120, a false low at 60 for 35 minutes, then straight back to 120.
        var glucose = Trace(T0 - 60 * Minute, T0 + 120 * Minute, m => m is >= 0 and < 35 ? 60 : 120);
        var dip = IntervalSet.Of(T0, T0 + 35 * Minute);

        Find(glucose).Should().ContainSingle("unexcluded, the recovery is a climb of 60 from a stable low")
            .Which.Mills.Should().Be(T0 + 30 * Minute);
        Find(glucose, exclusions: dip).Should().BeEmpty("the low is excluded, so the climb has no stable start");
    }

    [Fact]
    public void ARise_WithCarbsTwentyMinutesLater_IsNotAnchored()
    {
        Find(UnannouncedMeal(), carbs: [new CarbEvent(T0 + 20 * Minute, 45)]).Should().BeEmpty();
        Find(UnannouncedMeal(), carbs: [new CarbEvent(T0 + 31 * Minute, 45)])
            .Should().ContainSingle("a carb entry over half an hour away does not announce it");
    }

    [Fact]
    public void ARiseFromAFall_HasNoStableStart()
    {
        var rebound = Trace(T0 - 60 * Minute, T0 + 120 * Minute, m => m <= 0 ? 110 - m : 110 + 3 * m);

        Find(rebound).Should().BeEmpty();
    }

    [Fact]
    public void ARiseAcrossAGap_IsNotReadAcrossIt()
    {
        var glucose = UnannouncedMeal().Where(g => g.Mills <= T0 || g.Mills > T0 + 20 * Minute).ToList();

        Find(glucose).Should().BeEmpty();
    }
}
