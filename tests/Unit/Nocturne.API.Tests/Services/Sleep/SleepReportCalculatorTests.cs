using FluentAssertions;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Sleep.Report;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.API.Tests.Services.Sleep;

[Trait("Category", "Unit")]
public class SleepReportCalculatorTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    private static SleepSession MakeSession(DateTime? start = null)
    {
        var s = start ?? new DateTime(2026, 5, 16, 23, 0, 0, DateTimeKind.Utc);
        return new SleepSession { StartTime = s, EndTime = s.AddHours(8) };
    }

    private static SensorGlucose MakeGlucose(DateTime timestamp, double mgdl) =>
        new() { Timestamp = timestamp, Mgdl = mgdl };

    // ── Stage Breakdown ───────────────────────────────────────────────────

    [Fact]
    public void ComputeStageBreakdown_UsesSummaryFields_WhenPopulated()
    {
        var session = new SleepSession
        {
            StartTime    = DateTime.UtcNow,
            EndTime      = DateTime.UtcNow.AddHours(8),
            DeepSleepMs  = 90  * 60 * 1000L,
            RemSleepMs   = 100 * 60 * 1000L,
            LightSleepMs = 220 * 60 * 1000L,
            TotalAwakeMs = 30  * 60 * 1000L,
        };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeStageBreakdown(session);

        result.DeepMinutes.Should().Be(90);
        result.RemMinutes.Should().Be(100);
        result.LightMinutes.Should().Be(220);
        result.AwakeMinutes.Should().Be(30);
        result.TotalMinutes.Should().Be(440);
        result.DeepPct.Should().BeApproximately(90.0 / 440 * 100, 0.01);
    }

    [Fact]
    public void ComputeStageBreakdown_DerivesFromStages_WhenSummaryFieldsNull()
    {
        var now = new DateTime(2026, 5, 16, 23, 0, 0, DateTimeKind.Utc);
        var session = new SleepSession
        {
            StartTime    = now,
            EndTime      = now.AddHours(8),
            DeepSleepMs  = null,
            RemSleepMs   = null,
            LightSleepMs = null,
            TotalAwakeMs = null,
            Stages =
            [
                new SleepStageInterval { StartTime = now,                  EndTime = now.AddMinutes(30),  Stage = SleepStageType.Light },
                new SleepStageInterval { StartTime = now.AddMinutes(30),   EndTime = now.AddMinutes(120), Stage = SleepStageType.Deep  },
                new SleepStageInterval { StartTime = now.AddMinutes(120),  EndTime = now.AddMinutes(180), Stage = SleepStageType.Rem   },
            ],
        };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeStageBreakdown(session);

        result.LightMinutes.Should().Be(30);
        result.DeepMinutes.Should().Be(90);
        result.RemMinutes.Should().Be(60);
        result.AwakeMinutes.Should().Be(0);
        result.TotalMinutes.Should().Be(180);
    }

    // ── Overnight TIR ─────────────────────────────────────────────────────

    [Fact]
    public void ComputeOvernightTir_ReturnsNull_WhenNoGlucoseData()
    {
        var session = MakeSession();
        var result = API.Services.Sleep.SleepReportCalculator.ComputeOvernightTir(session, []);
        result.Should().BeNull();
    }

    [Fact]
    public void ComputeOvernightTir_ComputesRanges_UsingClinicalThresholds()
    {
        var session = MakeSession();
        var readings = new[]
        {
            MakeGlucose(session.StartTime.AddMinutes(10), 50),   // very low
            MakeGlucose(session.StartTime.AddMinutes(20), 65),   // low
            MakeGlucose(session.StartTime.AddMinutes(30), 120),  // in range
            MakeGlucose(session.StartTime.AddMinutes(40), 120),  // in range
            MakeGlucose(session.StartTime.AddMinutes(50), 200),  // high
            MakeGlucose(session.StartTime.AddMinutes(60), 260),  // very high
        };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeOvernightTir(session, readings);

        result.Should().NotBeNull();
        result!.VeryLowPct.Should().BeApproximately(100.0 / 6, 0.01);
        result.LowPct.Should().BeApproximately(100.0 / 6, 0.01);
        result.InRangePct.Should().BeApproximately(200.0 / 6, 0.01);
        result.HighPct.Should().BeApproximately(100.0 / 6, 0.01);
        result.VeryHighPct.Should().BeApproximately(100.0 / 6, 0.01);
        result.MeanBg.Should().Be((int)Math.Round((50 + 65 + 120 + 120 + 200 + 260) / 6.0));
    }

    // ── Hypo Events ───────────────────────────────────────────────────────

    [Fact]
    public void ComputeHypoEvents_ReturnsEmpty_WhenNoLowReadings()
    {
        var session = MakeSession();
        var glucose = new[] { MakeGlucose(session.StartTime.AddMinutes(10), 85) };
        var result = API.Services.Sleep.SleepReportCalculator.ComputeHypoEvents(session, glucose, []);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeHypoEvents_DetectsContiguousRun_AndTagsSeverity()
    {
        var session = MakeSession();
        var t0 = session.StartTime.AddMinutes(60);
        var glucose = new[]
        {
            MakeGlucose(t0,                65),  // low
            MakeGlucose(t0.AddMinutes(5),  62),  // low (nadir)
            MakeGlucose(t0.AddMinutes(10), 68),  // still low
            MakeGlucose(t0.AddMinutes(15), 75),  // recovered
        };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeHypoEvents(session, glucose, []);

        result.Should().HaveCount(1);
        result[0].LowestBg.Should().Be(62);
        result[0].Severity.Should().Be(SleepHypoSeverity.Low);
        result[0].DurationMinutes.Should().Be(10);
        result[0].Stage.Should().Be(SleepStageType.Unknown);
    }

    [Fact]
    public void ComputeHypoEvents_MarksVeryLow_WhenBelowFiftyFour()
    {
        var session = MakeSession();
        var t0 = session.StartTime.AddMinutes(120);
        var glucose = new[]
        {
            MakeGlucose(t0,               50),
            MakeGlucose(t0.AddMinutes(5), 71),
        };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeHypoEvents(session, glucose, []);

        result[0].Severity.Should().Be(SleepHypoSeverity.VeryLow);
    }

    [Fact]
    public void ComputeHypoEvents_TagsStage_FromStageIntervals()
    {
        var session = MakeSession();
        var t0 = session.StartTime.AddMinutes(90);
        var glucose = new[]
        {
            MakeGlucose(t0,               65),
            MakeGlucose(t0.AddMinutes(5), 71),
        };
        var stages = new[]
        {
            new SleepStageInterval
            {
                StartTime = session.StartTime.AddMinutes(60),
                EndTime   = session.StartTime.AddMinutes(120),
                Stage     = SleepStageType.Deep,
            },
        };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeHypoEvents(session, glucose, stages);

        result[0].Stage.Should().Be(SleepStageType.Deep);
    }
}
