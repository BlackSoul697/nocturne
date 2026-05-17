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

    // ── Dawn Phenomenon ───────────────────────────────────────────────────

    [Fact]
    public void ComputeDawnPhenomenon_ReturnsNull_WhenFewerThanFourReadings()
    {
        var session = MakeSession();
        var glucose = new[]
        {
            MakeGlucose(session.EndTime.AddMinutes(-90), 100),
            MakeGlucose(session.EndTime.AddMinutes(-60), 110),
        };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeDawnPhenomenon(session, glucose);

        result.Should().BeNull();
    }

    [Fact]
    public void ComputeDawnPhenomenon_ComputesDeltaAndRate_ForPositiveRise()
    {
        var session = MakeSession();
        var glucose = new[]
        {
            MakeGlucose(session.EndTime.AddMinutes(-115), 105),
            MakeGlucose(session.EndTime.AddMinutes(-110), 98),   // trough
            MakeGlucose(session.EndTime.AddMinutes(-60),  115),
            MakeGlucose(session.EndTime.AddMinutes(-10),  140),  // peak
        };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeDawnPhenomenon(session, glucose);

        result.Should().NotBeNull();
        result!.TroughBg.Should().Be(98);
        result.PeakBg.Should().Be(140);
        result.DeltaBg.Should().Be(42);
        result.RateOfClimbPerHour.Should().BePositive();
    }

    [Fact]
    public void ComputeDawnPhenomenon_ReportsNegativeDelta_WhenGlucoseDeclining()
    {
        var session = MakeSession();
        var glucose = new[]
        {
            MakeGlucose(session.EndTime.AddMinutes(-115), 145), // peak (earlier)
            MakeGlucose(session.EndTime.AddMinutes(-90),  130),
            MakeGlucose(session.EndTime.AddMinutes(-45),  110),
            MakeGlucose(session.EndTime.AddMinutes(-10),  98),  // trough (later)
        };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeDawnPhenomenon(session, glucose);

        result.Should().NotBeNull();
        result!.DeltaBg.Should().BeNegative();
        result.RateOfClimbPerHour.Should().BeNegative();
    }

    // ── Wake Events ───────────────────────────────────────────────────────

    [Fact]
    public void ComputeWakeEvents_ExtractsAwakeIntervals()
    {
        var session = MakeSession();
        var stages = new[]
        {
            new SleepStageInterval { StartTime = session.StartTime,                EndTime = session.StartTime.AddMinutes(20),  Stage = SleepStageType.Awake },
            new SleepStageInterval { StartTime = session.StartTime.AddMinutes(20), EndTime = session.StartTime.AddMinutes(100), Stage = SleepStageType.Deep  },
            new SleepStageInterval { StartTime = session.StartTime.AddMinutes(100),EndTime = session.StartTime.AddMinutes(110), Stage = SleepStageType.Awake },
        };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeWakeEvents(session, stages, []);

        result.Should().HaveCount(2);
        result[0].DurationMinutes.Should().Be(20);
        result[0].IsPreSleep.Should().BeTrue();
        result[1].DurationMinutes.Should().Be(10);
        result[1].IsPreSleep.Should().BeFalse();
    }

    [Fact]
    public void ComputeWakeEvents_AttachesNearestGlucose_WhenWithinFifteenMinutes()
    {
        var session = MakeSession();
        var wakeStart = session.StartTime.AddMinutes(100);
        var stages = new[]
        {
            new SleepStageInterval { StartTime = wakeStart, EndTime = wakeStart.AddMinutes(10), Stage = SleepStageType.Awake },
        };
        var glucose = new[] { MakeGlucose(wakeStart.AddMinutes(3), 88) };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeWakeEvents(session, stages, glucose);

        result[0].BgAtStart.Should().Be(88);
    }

    [Fact]
    public void ComputeWakeEvents_NullsBg_WhenNearestGlucoseExceedsFifteenMinutes()
    {
        var session = MakeSession();
        var wakeStart = session.StartTime.AddMinutes(100);
        var stages = new[]
        {
            new SleepStageInterval { StartTime = wakeStart, EndTime = wakeStart.AddMinutes(10), Stage = SleepStageType.Awake },
        };
        var glucose = new[] { MakeGlucose(wakeStart.AddMinutes(20), 88) };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeWakeEvents(session, stages, glucose);

        result[0].BgAtStart.Should().BeNull();
    }

    // ── Score Resolution ──────────────────────────────────────────────────

    [Fact]
    public void ResolveScore_UsesDeviceScore_WhenPresent()
    {
        var session = new SleepSession { SleepScore = 82 };
        var (score, source) = API.Services.Sleep.SleepReportCalculator.ResolveScore(session, 0, new SleepStageBreakdown());
        score.Should().Be(82);
        source.Should().Be(SleepScoreSource.Device);
    }

    [Fact]
    public void ResolveScore_ComputesFallback_WhenScoreNull()
    {
        var session = new SleepSession { SleepScore = null };
        var breakdown = new SleepStageBreakdown
        {
            DeepMinutes = 90, RemMinutes = 100, LightMinutes = 230, AwakeMinutes = 20, TotalMinutes = 440,
        };
        var (score, source) = API.Services.Sleep.SleepReportCalculator.ResolveScore(session, hypoCount: 0, breakdown);
        score.Should().BeInRange(0, 100);
        source.Should().Be(SleepScoreSource.Computed);
    }

    // ── Night Summary ─────────────────────────────────────────────────────

    [Fact]
    public void ComputeNightSummary_PopulatesFields()
    {
        var sessionId = Guid.NewGuid();
        var session = MakeSession();
        session.Id           = sessionId.ToString();
        session.DeepSleepMs  = 90  * 60_000L;
        session.RemSleepMs   = 100 * 60_000L;
        session.LightSleepMs = 220 * 60_000L;
        session.TotalAwakeMs = 30  * 60_000L;
        session.SleepScore   = 75;

        var result = API.Services.Sleep.SleepReportCalculator.ComputeNightSummary(session, []);

        result.SessionId.Should().Be(sessionId);
        result.SleepScore.Should().Be(75);
        result.ScoreSource.Should().Be(SleepScoreSource.Device);
        result.DeepMinutes.Should().Be(90);
        result.HypoCount.Should().Be(0);
        result.LowestBg.Should().BeNull();
    }

    [Fact]
    public void ComputeNightSummary_ComputesScore_WhenDeviceScoreAbsent()
    {
        var session = MakeSession();
        session.Id           = Guid.NewGuid().ToString();
        session.DeepSleepMs  = 90  * 60_000L;
        session.RemSleepMs   = 100 * 60_000L;
        session.LightSleepMs = 220 * 60_000L;
        session.TotalAwakeMs = 30  * 60_000L;
        session.SleepScore   = null;

        var result = API.Services.Sleep.SleepReportCalculator.ComputeNightSummary(session, []);

        result.ScoreSource.Should().Be(SleepScoreSource.Computed);
        result.SleepScore.Should().NotBeNull();
        result.SleepScore.Should().BeInRange(0, 100);
    }

    // ── Deduplication ─────────────────────────────────────────────────────

    [Fact]
    public void DeduplicateToOnePerNight_PicksLongestSession()
    {
        var night   = new DateTime(2026, 5, 16, 23, 0, 0, DateTimeKind.Utc);
        var shorter = new SleepSession { StartTime = night, EndTime = night.AddHours(6), TotalSleepMs = 6 * 3_600_000L, Source = SleepSource.Samsung };
        var longer  = new SleepSession { StartTime = night, EndTime = night.AddHours(8), TotalSleepMs = 8 * 3_600_000L, Source = SleepSource.Oura };

        var result = API.Services.Sleep.SleepReportCalculator.DeduplicateToOnePerNight([shorter, longer]);

        result.Should().HaveCount(1);
        result[0].Source.Should().Be(SleepSource.Oura);
    }

    [Fact]
    public void DeduplicateToOnePerNight_TieBreaksBySourcePriority()
    {
        var night   = new DateTime(2026, 5, 16, 23, 0, 0, DateTimeKind.Utc);
        var oura    = new SleepSession { StartTime = night, EndTime = night.AddHours(8), TotalSleepMs = 8 * 3_600_000L, Source = SleepSource.Oura };
        var samsung = new SleepSession { StartTime = night, EndTime = night.AddHours(8), TotalSleepMs = 8 * 3_600_000L, Source = SleepSource.Samsung };

        var result = API.Services.Sleep.SleepReportCalculator.DeduplicateToOnePerNight([samsung, oura]);

        result[0].Source.Should().Be(SleepSource.Oura);
    }

    // ── Trends Summary ────────────────────────────────────────────────────

    [Fact]
    public void ComputeTrendsSummary_ComputesMeans()
    {
        var nights = new[]
        {
            new SleepNightSummary { SleepScore = 70, OvernightTirPct = 80, DeepMinutes = 90, SleepMinutes = 440, HypoCount = 0 },
            new SleepNightSummary { SleepScore = 80, OvernightTirPct = 90, DeepMinutes = 110, SleepMinutes = 460, HypoCount = 1 },
        };

        var result = API.Services.Sleep.SleepReportCalculator.ComputeTrendsSummary(nights);

        result.NightCount.Should().Be(2);
        result.MeanScore.Should().BeApproximately(75, 0.01);
        result.MeanTirPct.Should().BeApproximately(85, 0.01);
        result.TotalHypoCount.Should().Be(1);
        result.NightsWithHypoPct.Should().BeApproximately(50, 0.01);
    }

    [Fact]
    public void ComputeTrendsSummary_Computes7dVsPrior7dDeltas()
    {
        // 14 nights: first 7 score=60, last 7 score=80 → delta = +20
        var nights = Enumerable.Range(0, 14).Select(i => new SleepNightSummary
        {
            SleepScore      = i < 7 ? 60 : 80,
            OvernightTirPct = 85,
            DeepMinutes     = 90,
            SleepMinutes    = 440,
            HypoCount       = 0,
        }).ToArray();

        var result = API.Services.Sleep.SleepReportCalculator.ComputeTrendsSummary(nights);

        result.Last7dVsPrior7d.ScoreDelta.Should().BeApproximately(20, 0.01);
    }
}
