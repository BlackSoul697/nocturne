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
}
