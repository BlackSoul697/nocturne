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
        var (clipped, recorded, unknown) = NetInsulinAboveSchedule.ClipTempBasals(
            [Temp(0, 30, 2.0), Temp(5, 30, 0.0)]);

        clipped.Select(t => t.EndTimestamp).Should().Equal(T0.AddMinutes(5), T0.AddMinutes(35));
        recorded.Intervals.Should().Equal(new MillsInterval(T0Mills, T0Mills + 35 * Minute));
        unknown.IsEmpty.Should().BeTrue();

        // The series IBasalSeriesBuilder builds from the clipped temps: one point per temp.
        var delivered = NetInsulinAboveSchedule.DeliveredFromSeries(
            [Point(0, 2.0, BasalDeliveryOrigin.Algorithm), Point(5, 0.0, BasalDeliveryOrigin.Algorithm)],
            T0Mills + 35 * Minute, recorded, unknown);
        var net = new NetInsulinAboveSchedule(delivered, [new(T0Mills, T0Mills + Hour, 1.0)], [])
            .Over(T0Mills, T0Mills + Hour);

        net.AlgorithmTemp.Should().BeApproximately(1.0 / 12 - 0.5, 1e-9, "+1 U/h for 5 min, then a zero-temp for 30");
        net.NetLoopCorrection.Should().BeApproximately(1.0 / 12 - 0.5, 1e-9);
        net.CoveredMs.Should().Be(35 * Minute, "past the last temp there is no delivery record");
    }

    [Fact]
    public void PercentTempWithNoRate_IsUnknownDeliveryNotZero()
    {
        var (_, recorded, unknown) = NetInsulinAboveSchedule.ClipTempBasals(
            [Temp(0, 10, 2.0), Temp(10, 20, null)]);

        unknown.Intervals.Should().Equal(new MillsInterval(T0Mills + 10 * Minute, T0Mills + 30 * Minute));

        // The series shows the schedule under a temp of unknown rate.
        var delivered = NetInsulinAboveSchedule.DeliveredFromSeries(
            [Point(0, 2.0, BasalDeliveryOrigin.Algorithm), Point(10, 1.0, BasalDeliveryOrigin.Inferred), Point(30, 1.0, BasalDeliveryOrigin.Inferred)],
            T0Mills + Hour, recorded, unknown);

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
        var (_, recorded, unknown) = NetInsulinAboveSchedule.ClipTempBasals([Temp(0, 10, 2.0), Temp(50, 10, 2.0)]);

        var delivered = NetInsulinAboveSchedule.DeliveredFromSeries(
            [Point(0, 2.0, BasalDeliveryOrigin.Algorithm), Point(50, 2.0, BasalDeliveryOrigin.Algorithm)],
            T0Mills + Hour, recorded, unknown);

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

    private static BasalPoint Point(int minute, double rate, BasalDeliveryOrigin origin) =>
        new() { Timestamp = T0Mills + minute * Minute, Rate = rate, Origin = origin };
}
