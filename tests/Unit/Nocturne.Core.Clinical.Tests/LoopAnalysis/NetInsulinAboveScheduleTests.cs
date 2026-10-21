using Nocturne.Core.Clinical.LoopAnalysis;
using Nocturne.Core.Models.Basal;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Clinical.Tests.LoopAnalysis;

public class NetInsulinAboveScheduleTests
{
    private const long Minute = 60_000;
    private const long Hour = 60 * Minute;
    private static readonly DateTime T0 = new(2024, 6, 3, 10, 0, 0, DateTimeKind.Utc);
    private static readonly long T0Mills = new DateTimeOffset(T0).ToUnixTimeMilliseconds();

    private static TempBasal Temp(int startMinute, int? durationMinutes, double? rate, TempBasalOrigin origin = TempBasalOrigin.Algorithm) => new()
    {
        StartTimestamp = T0.AddMinutes(startMinute),
        EndTimestamp = durationMinutes is { } d ? T0.AddMinutes(startMinute + d) : null,
        Rate = rate,
        Origin = origin,
        Device = "pump",
    };

    [Fact]
    public void OverlappingAlgorithmTemps_EachEndsWhereTheNextStarts()
    {
        var (clipped, evidence) = NetInsulinAboveSchedule.ClipTempBasals(
            [Temp(0, 30, 2.0), Temp(5, 30, 0.0)], []);

        clipped.Select(t => t.EndTimestamp).Should().Equal(T0.AddMinutes(5), T0.AddMinutes(35));
        evidence.Recorded.Intervals.Should().Equal(new MillsInterval(T0Mills, T0Mills + 35 * Minute));
        evidence.Unknown.IsEmpty.Should().BeTrue();

        // The series IBasalSeriesBuilder builds from the clipped temps: one point per temp.
        var delivered = NetInsulinAboveSchedule.DeliveredFromSeries(
            [Point(0, 2.0, BasalDeliveryOrigin.Algorithm), Point(5, 0.0, BasalDeliveryOrigin.Algorithm)],
            T0Mills + 35 * Minute, evidence);
        var net = new NetInsulinAboveSchedule(delivered, [new(T0Mills, T0Mills + Hour, 1.0)], [])
            .Over(T0Mills, T0Mills + Hour);

        net.AlgorithmTemp.Should().BeApproximately(1.0 / 12 - 0.5, 1e-9, "+1 U/h for 5 min, then a zero-temp for 30");
        net.NetLoopCorrection.Should().BeApproximately(1.0 / 12 - 0.5, 1e-9);
        net.CoveredMs.Should().Be(35 * Minute, "past the last temp there is no delivery record");
    }

    [Fact]
    public void PercentTempWithNoRate_IsUnknownDeliveryNotZero()
    {
        var (_, evidence) = NetInsulinAboveSchedule.ClipTempBasals(
            [Temp(0, 10, 2.0), Temp(10, 20, null)], [T0Mills + Hour]);

        evidence.Unknown.Intervals.Should().Equal(new MillsInterval(T0Mills + 10 * Minute, T0Mills + 30 * Minute));

        // The series shows the schedule under a temp of unknown rate.
        var delivered = NetInsulinAboveSchedule.DeliveredFromSeries(
            [Point(0, 2.0, BasalDeliveryOrigin.Algorithm), Point(10, 1.0, BasalDeliveryOrigin.Inferred), Point(30, 1.0, BasalDeliveryOrigin.Inferred)],
            T0Mills + Hour, evidence);

        delivered.Should().Equal(
            new DeliveredBasalSegment(T0Mills, T0Mills + 10 * Minute, 2.0, BasalDeliveryOrigin.Algorithm),
            new DeliveredBasalSegment(T0Mills + 30 * Minute, T0Mills + Hour, 1.0, BasalDeliveryOrigin.Inferred));

        var net = new NetInsulinAboveSchedule(delivered, [new(T0Mills, T0Mills + Hour, 1.0)], []).Over(T0Mills, T0Mills + Hour);
        net.CoveredMs.Should().Be(40 * Minute);
        net.AlgorithmTemp.Should().BeApproximately(1.0 / 6, 1e-9);
    }

    [Fact]
    public void RecordedPoint_DoesNotRunOnAcrossAnUnfilledGap()
    {
        var (_, evidence) = NetInsulinAboveSchedule.ClipTempBasals([Temp(0, 10, 2.0), Temp(50, 10, 2.0)], []);

        var delivered = NetInsulinAboveSchedule.DeliveredFromSeries(
            [Point(0, 2.0, BasalDeliveryOrigin.Algorithm), Point(50, 2.0, BasalDeliveryOrigin.Algorithm)],
            T0Mills + Hour, evidence);

        delivered.Sum(d => d.End - d.Start).Should().Be(20 * Minute);
    }

    [Fact]
    public void Boluses_AreSplitByWhoGaveThem()
    {
        var net = new NetInsulinAboveSchedule([], [],
            [new(T0Mills, 0.3, true), new(T0Mills + Minute, 2.0, false), new(T0Mills + Hour, 5, false)])
            .Over(T0Mills, T0Mills + Hour);

        net.AutomaticBolus.Should().Be(0.3);
        net.ManualBolus.Should().Be(2.0, "the interval is half-open");
        net.NetLoopCorrection.Should().Be(0.3);
        net.Total.Should().Be(2.3);
    }

    [Fact]
    public void ManualTempSuspendAndInferred_AreTheirOwnComponents()
    {
        var net = new NetInsulinAboveSchedule(
            [
                new(T0Mills, T0Mills + 15 * Minute, 3.0, BasalDeliveryOrigin.Manual),
                new(T0Mills + 15 * Minute, T0Mills + 30 * Minute, 0, BasalDeliveryOrigin.Suspended),
                new(T0Mills + 30 * Minute, T0Mills + 45 * Minute, 1.0, BasalDeliveryOrigin.Scheduled),
                new(T0Mills + 45 * Minute, T0Mills + Hour, 1.0, BasalDeliveryOrigin.Inferred),
            ],
            [new(T0Mills, T0Mills + Hour, 1.0)], [])
            .Over(T0Mills, T0Mills + Hour);

        net.ManualTemp.Should().Be(0.5);
        net.Suspend.Should().Be(-0.25);
        net.Inferred.Should().Be(0);
        net.NetLoopCorrection.Should().Be(0);
    }

    [Fact]
    public void OverrideScaleFactor_ScalesTheBaseline()
    {
        var baseline = NetInsulinAboveSchedule.ScheduledBaseline(
            [new BasalSegment(T0Mills, T0Mills + Hour, 1.0, 1.0, "Default")],
            [new ScaleFactorInterval(T0Mills + 20 * Minute, T0Mills + 40 * Minute, 0.5)]);

        baseline.Should().Equal(
            new RateSegment(T0Mills, T0Mills + 20 * Minute, 1.0),
            new RateSegment(T0Mills + 20 * Minute, T0Mills + 40 * Minute, 0.5),
            new RateSegment(T0Mills + 40 * Minute, T0Mills + Hour, 1.0));

        var net = new NetInsulinAboveSchedule(
            [new(T0Mills, T0Mills + Hour, 0.5, BasalDeliveryOrigin.Algorithm)], baseline, [])
            .Over(T0Mills, T0Mills + Hour);
        net.AlgorithmTemp.Should().BeApproximately(-1.0 / 3, 1e-9, "the override's 0.5 U/h is not the loop's doing");
    }

    [Fact]
    public void ScheduleFill_HoldsOnlyBetweenNearbyPumpEvidence()
    {
        // Temps for the first two hours, a 2-hour gap, an hour of temps, then an 8-hour outage
        // and one more temp. The series fills every gap from the schedule.
        var (_, evidence) = NetInsulinAboveSchedule.ClipTempBasals(
            [Temp(0, 120, 2.0), Temp(240, 60, 2.0), Temp(780, 30, 2.0)], []);

        var delivered = NetInsulinAboveSchedule.DeliveredFromSeries(
            [
                Point(-300, 1.0, BasalDeliveryOrigin.Inferred),
                Point(0, 2.0, BasalDeliveryOrigin.Algorithm),
                Point(120, 1.0, BasalDeliveryOrigin.Inferred),
                Point(240, 2.0, BasalDeliveryOrigin.Algorithm),
                Point(300, 1.0, BasalDeliveryOrigin.Inferred),
                Point(780, 2.0, BasalDeliveryOrigin.Algorithm),
                Point(810, 1.0, BasalDeliveryOrigin.Inferred),
            ],
            T0Mills + 1200 * Minute, evidence);

        delivered.Where(d => d.Origin == BasalDeliveryOrigin.Inferred)
            .Select(d => (d.Start, d.End))
            .Should().Equal([(T0Mills + 120 * Minute, T0Mills + 240 * Minute)],
                "a 2-hour gap between temps ran the schedule; before the first temp, the 8-hour outage and after the last there is no evidence");
    }

    [Fact]
    public void ApsSnapshots_AreEvidenceTheScheduleRan()
    {
        var snapshots = Enumerable.Range(0, 13).Select(i => T0Mills + i * 5 * Minute);
        var (_, evidence) = NetInsulinAboveSchedule.ClipTempBasals([], snapshots);

        evidence.Inferable.Intervals.Should().Equal(new MillsInterval(T0Mills, T0Mills + Hour));
    }

    [Fact]
    public void OverlappingOverrides_TheLatestStartedIsInForce()
    {
        var baseline = NetInsulinAboveSchedule.ScheduledBaseline(
            [new BasalSegment(T0Mills, T0Mills + Hour, 1.0, 1.0, "Default")],
            [
                new ScaleFactorInterval(T0Mills, T0Mills + 40 * Minute, 0.5),
                new ScaleFactorInterval(T0Mills + 10 * Minute, T0Mills + 20 * Minute, 2.0),
            ]);

        baseline.Should().Equal(
            new RateSegment(T0Mills, T0Mills + 10 * Minute, 0.5),
            new RateSegment(T0Mills + 10 * Minute, T0Mills + 20 * Minute, 2.0),
            new RateSegment(T0Mills + 20 * Minute, T0Mills + 40 * Minute, 0.5),
            new RateSegment(T0Mills + 40 * Minute, T0Mills + Hour, 1.0));
    }

    private static BasalPoint Point(int minute, double rate, BasalDeliveryOrigin origin) =>
        new() { Timestamp = T0Mills + minute * Minute, Rate = rate, Origin = origin };
}
