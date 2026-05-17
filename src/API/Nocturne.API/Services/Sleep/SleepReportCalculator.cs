using Nocturne.Core.Constants;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Sleep.Report;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.Sleep;

/// <summary>
/// Pure static computation helpers for sleep report statistics.
/// No dependencies — all inputs are passed as parameters.
/// </summary>
internal static class SleepReportCalculator
{
    private static readonly TimeSpan GlucoseStalenessLimit = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DawnWindowSize        = TimeSpan.FromHours(2);
    private const int DawnMinReadings = 4;

    private static readonly SleepSource[] SourcePriority =
    [
        SleepSource.Oura, SleepSource.Garmin, SleepSource.Apple,
        SleepSource.Samsung, SleepSource.Fitbit, SleepSource.Manual, SleepSource.Google,
    ];

    // ── Stage Breakdown ────────────────────────────────────────────────────

    internal static SleepStageBreakdown ComputeStageBreakdown(SleepSession session)
    {
        int deep, rem, light, awake;

        if (session.DeepSleepMs.HasValue && session.RemSleepMs.HasValue
            && session.LightSleepMs.HasValue && session.TotalAwakeMs.HasValue)
        {
            deep  = (int)(session.DeepSleepMs.Value  / 60_000);
            rem   = (int)(session.RemSleepMs.Value   / 60_000);
            light = (int)(session.LightSleepMs.Value / 60_000);
            awake = (int)(session.TotalAwakeMs.Value / 60_000);
        }
        else
        {
            deep = rem = light = awake = 0;
            foreach (var stage in session.Stages ?? [])
            {
                var mins = (int)(stage.EndTime - stage.StartTime).TotalMinutes;
                switch (stage.Stage)
                {
                    case SleepStageType.Deep:                                     deep  += mins; break;
                    case SleepStageType.Rem:                                      rem   += mins; break;
                    case SleepStageType.Light: case SleepStageType.Asleep:        light += mins; break;
                    case SleepStageType.Awake: case SleepStageType.AwakeInBed:
                    case SleepStageType.Restless:                                 awake += mins; break;
                }
            }
        }

        var total = deep + rem + light + awake;
        return new SleepStageBreakdown
        {
            DeepMinutes  = deep,
            RemMinutes   = rem,
            LightMinutes = light,
            AwakeMinutes = awake,
            TotalMinutes = total,
            DeepPct  = total > 0 ? deep  * 100.0 / total : 0,
            RemPct   = total > 0 ? rem   * 100.0 / total : 0,
            LightPct = total > 0 ? light * 100.0 / total : 0,
            AwakePct = total > 0 ? awake * 100.0 / total : 0,
        };
    }

    // ── Overnight TIR ─────────────────────────────────────────────────────

    internal static SleepOvernightTir? ComputeOvernightTir(
        SleepSession session, IEnumerable<SensorGlucose> allGlucose)
    {
        var readings = allGlucose
            .Where(g => g.Timestamp >= session.StartTime && g.Timestamp <= session.EndTime)
            .ToList();

        if (readings.Count == 0) return null;

        int veryLow = 0, low = 0, inRange = 0, high = 0, veryHigh = 0;
        double sum = 0;

        foreach (var g in readings)
        {
            sum += g.Mgdl;
            if      (g.Mgdl <= ApplicationConstants.ClinicalThresholds.VeryLow)  veryLow++;
            else if (g.Mgdl <= ApplicationConstants.ClinicalThresholds.Low)       low++;
            else if (g.Mgdl <= ApplicationConstants.ClinicalThresholds.High)      inRange++;
            else if (g.Mgdl <= ApplicationConstants.ClinicalThresholds.VeryHigh)  high++;
            else                                                                   veryHigh++;
        }

        var n = (double)readings.Count;
        return new SleepOvernightTir
        {
            VeryLowPct  = veryLow  / n * 100,
            LowPct      = low      / n * 100,
            InRangePct  = inRange  / n * 100,
            HighPct     = high     / n * 100,
            VeryHighPct = veryHigh / n * 100,
            MeanBg      = (int)Math.Round(sum / n),
        };
    }

    // ── Hypo Events ───────────────────────────────────────────────────────

    internal static IReadOnlyList<SleepHypoEvent> ComputeHypoEvents(
        SleepSession session,
        IEnumerable<SensorGlucose> allGlucose,
        IEnumerable<SleepStageInterval> stages)
    {
        var readings = allGlucose
            .Where(g => g.Timestamp >= session.StartTime && g.Timestamp <= session.EndTime)
            .OrderBy(g => g.Timestamp)
            .ToList();

        var stageList = stages.ToList();
        var events    = new List<SleepHypoEvent>();
        SensorGlucose? runStart = null;
        SensorGlucose? nadir    = null;
        SensorGlucose? prev     = null;

        foreach (var g in readings)
        {
            if (g.Mgdl < ApplicationConstants.ClinicalThresholds.Low)
            {
                runStart ??= g;
                if (nadir == null || g.Mgdl < nadir.Mgdl) nadir = g;
            }
            else if (runStart != null && nadir != null && prev != null)
            {
                events.Add(BuildHypoEvent(runStart, prev, nadir, stageList));
                runStart = nadir = null;
            }
            prev = g;
        }

        if (runStart != null && nadir != null && prev != null)
            events.Add(BuildHypoEvent(runStart, prev, nadir, stageList));

        return events;
    }

    private static SleepHypoEvent BuildHypoEvent(
        SensorGlucose start, SensorGlucose end, SensorGlucose nadir,
        IEnumerable<SleepStageInterval> stages)
    {
        var stage = stages.FirstOrDefault(s =>
            s.StartTime <= nadir.Timestamp && s.EndTime >= nadir.Timestamp)?.Stage
            ?? SleepStageType.Unknown;

        return new SleepHypoEvent
        {
            StartAt         = start.Timestamp,
            EndAt           = end.Timestamp,
            DurationMinutes = (int)(end.Timestamp - start.Timestamp).TotalMinutes,
            LowestBg        = (int)Math.Round(nadir.Mgdl),
            Stage           = stage,
            Severity        = nadir.Mgdl <= ApplicationConstants.ClinicalThresholds.VeryLow
                                ? SleepHypoSeverity.VeryLow
                                : SleepHypoSeverity.Low,
        };
    }

    // ── Dawn Phenomenon ───────────────────────────────────────────────────

    internal static SleepDawnPhenomenon? ComputeDawnPhenomenon(
        SleepSession session, IEnumerable<SensorGlucose> allGlucose)
    {
        var windowStart = session.EndTime - DawnWindowSize;
        var readings = allGlucose
            .Where(g => g.Timestamp >= windowStart && g.Timestamp <= session.EndTime)
            .OrderBy(g => g.Timestamp)
            .ToList();

        if (readings.Count < DawnMinReadings) return null;

        var troughReading = readings.MinBy(g => g.Mgdl)!;
        var peakReading   = readings.MaxBy(g => g.Mgdl)!;

        var trough = (int)Math.Round(troughReading.Mgdl);
        var peak   = (int)Math.Round(peakReading.Mgdl);
        var absDelta = peak - trough;

        var hours = Math.Abs((peakReading.Timestamp - troughReading.Timestamp).TotalHours);
        var signedDelta = peakReading.Timestamp >= troughReading.Timestamp ? absDelta : -absDelta;
        var rate  = hours > 0 ? signedDelta / hours : 0.0;

        return new SleepDawnPhenomenon
        {
            WindowStart        = windowStart,
            WindowEnd          = session.EndTime,
            TroughBg           = trough,
            PeakBg             = peak,
            DeltaBg            = signedDelta,
            RateOfClimbPerHour = rate,
        };
    }

    // ── Wake Events ───────────────────────────────────────────────────────

    internal static IReadOnlyList<SleepWakeEvent> ComputeWakeEvents(
        SleepSession session,
        IEnumerable<SleepStageInterval> stages,
        IEnumerable<SensorGlucose> allGlucose)
    {
        var awakeIntervals = stages
            .Where(s => s.Stage is SleepStageType.Awake or SleepStageType.AwakeInBed)
            .OrderBy(s => s.StartTime)
            .ToList();

        var glucose = allGlucose
            .Where(g => g.Timestamp >= session.StartTime && g.Timestamp <= session.EndTime)
            .OrderBy(g => g.Timestamp)
            .ToList();

        var sleepOnset = session.SleepLatencyMs.HasValue
            ? session.StartTime.AddMilliseconds(session.SleepLatencyMs.Value)
            : stages.Where(s => s.Stage is not SleepStageType.Awake and not SleepStageType.AwakeInBed)
                    .MinBy(s => s.StartTime)?.StartTime ?? session.StartTime;

        return awakeIntervals.Select(interval =>
        {
            var nearest = glucose.MinBy(g => Math.Abs((g.Timestamp - interval.StartTime).TotalSeconds));

            var bg = nearest != null
                && Math.Abs((nearest.Timestamp - interval.StartTime).TotalMinutes) <= GlucoseStalenessLimit.TotalMinutes
                ? (int?)Math.Round(nearest.Mgdl) : null;

            return new SleepWakeEvent
            {
                StartAt         = interval.StartTime,
                EndAt           = interval.EndTime,
                DurationMinutes = (int)(interval.EndTime - interval.StartTime).TotalMinutes,
                BgAtStart       = bg,
                IsPreSleep      = interval.EndTime <= sleepOnset,
                IsPostSleep     = interval.StartTime >= session.EndTime.AddMinutes(-5),
            };
        }).ToList();
    }

    // ── Score Resolution ──────────────────────────────────────────────────

    internal static (int Score, SleepScoreSource Source) ResolveScore(
        SleepSession session, int hypoCount, SleepStageBreakdown breakdown)
    {
        if (session.SleepScore.HasValue)
            return (session.SleepScore.Value, SleepScoreSource.Device);

        var total = (double)breakdown.TotalMinutes;
        if (total == 0) return (0, SleepScoreSource.Computed);

        var efficiency = (breakdown.DeepMinutes + breakdown.RemMinutes + breakdown.LightMinutes) / total;
        var deepFrac   = breakdown.DeepMinutes  / total;
        var remFrac    = breakdown.RemMinutes   / total;
        var disruption = Math.Min(20, breakdown.AwakeMinutes * 0.6 + hypoCount * 4);
        var raw        = 40 + efficiency * 25 + deepFrac * 90 + remFrac * 35 - disruption;
        var score      = (int)Math.Round(Math.Clamp(raw, 0, 100));

        return (score, SleepScoreSource.Computed);
    }

    // ── Night Summary ─────────────────────────────────────────────────────

    internal static SleepNightSummary ComputeNightSummary(
        SleepSession session, IEnumerable<SensorGlucose> sessionGlucose)
    {
        var glucose   = sessionGlucose.ToList();
        var breakdown = ComputeStageBreakdown(session);
        var hypos     = ComputeHypoEvents(session, glucose, session.Stages ?? []);
        var tir       = ComputeOvernightTir(session, glucose);
        var dawn      = ComputeDawnPhenomenon(session, glucose);
        var (score, scoreSource) = ResolveScore(session, hypos.Count, breakdown);

        _ = Guid.TryParse(session.Id, out var sessionId);

        return new SleepNightSummary
        {
            SessionId       = sessionId,
            Date            = session.StartTime.ToString("MMM d"),
            Weekday         = session.StartTime.DayOfWeek.ToString()[..3],
            InBedAt         = session.StartTime,
            WakeAt          = session.EndTime,
            SleepMinutes    = breakdown.DeepMinutes + breakdown.RemMinutes + breakdown.LightMinutes,
            DeepMinutes     = breakdown.DeepMinutes,
            RemMinutes      = breakdown.RemMinutes,
            LightMinutes    = breakdown.LightMinutes,
            AwakeMinutes    = breakdown.AwakeMinutes,
            SleepScore      = score == 0 && session.SleepScore == null ? null : score,
            ScoreSource     = scoreSource,
            OvernightTirPct = tir?.InRangePct,
            HypoCount       = hypos.Count,
            LowestBg        = hypos.Count > 0 ? hypos.Min(h => h.LowestBg) : null,
            DawnRiseDeltaMg = dawn?.DeltaBg,
        };
    }

    // ── Deduplication ─────────────────────────────────────────────────────

    internal static IReadOnlyList<SleepSession> DeduplicateToOnePerNight(
        IEnumerable<SleepSession> sessions)
    {
        return sessions
            .GroupBy(s => s.StartTime.Date)
            .Select(g => g
                .OrderByDescending(s => s.TotalSleepMs)
                .ThenBy(s => Array.IndexOf(SourcePriority, s.Source))
                .First())
            .OrderBy(s => s.StartTime)
            .ToList();
    }

    // ── Trends Summary ────────────────────────────────────────────────────

    internal static SleepTrendsSummary ComputeTrendsSummary(IReadOnlyList<SleepNightSummary> nights)
    {
        if (nights.Count == 0) return new SleepTrendsSummary();

        var scored     = nights.Where(n => n.SleepScore.HasValue).ToList();
        var tirNights  = nights.Where(n => n.OvernightTirPct.HasValue).ToList();
        var totalSleep = nights.Sum(n => n.SleepMinutes);
        var totalDeep  = nights.Sum(n => n.DeepMinutes);
        var totalRem   = nights.Sum(n => n.RemMinutes);

        var last7  = nights.TakeLast(7).ToList();
        var prior7 = nights.TakeLast(14).Take(7).ToList();

        static double? MeanScore(IList<SleepNightSummary> ns) =>
            ns.Any(n => n.SleepScore.HasValue)
                ? ns.Where(n => n.SleepScore.HasValue).Average(n => (double)n.SleepScore!.Value)
                : null;

        static double? MeanTir(IList<SleepNightSummary> ns) =>
            ns.Any(n => n.OvernightTirPct.HasValue)
                ? ns.Where(n => n.OvernightTirPct.HasValue).Average(n => n.OvernightTirPct!.Value)
                : null;

        var l7Score = MeanScore(last7);
        var p7Score = MeanScore(prior7);
        var l7Tir   = MeanTir(last7);
        var p7Tir   = MeanTir(prior7);
        var l7Deep  = last7.Any()  ? last7.Average(n => n.DeepMinutes)  : (double?)null;
        var p7Deep  = prior7.Any() ? prior7.Average(n => n.DeepMinutes) : (double?)null;

        return new SleepTrendsSummary
        {
            NightCount        = nights.Count,
            MeanScore         = scored.Count   > 0 ? scored.Average(n => (double)n.SleepScore!.Value) : null,
            MeanTirPct        = tirNights.Count > 0 ? tirNights.Average(n => n.OvernightTirPct!.Value) : null,
            MeanAsleepMinutes = nights.Average(n => n.SleepMinutes),
            MeanDeepPct       = totalSleep > 0 ? totalDeep * 100.0 / totalSleep : 0,
            MeanRemPct        = totalSleep > 0 ? totalRem  * 100.0 / totalSleep : 0,
            MeanDawnRiseMg    = nights.Any(n => n.DawnRiseDeltaMg.HasValue)
                                  ? nights.Where(n => n.DawnRiseDeltaMg.HasValue).Average(n => (double)n.DawnRiseDeltaMg!.Value)
                                  : null,
            TotalHypoCount    = nights.Sum(n => n.HypoCount),
            NightsWithHypoPct = nights.Count > 0 ? nights.Count(n => n.HypoCount > 0) * 100.0 / nights.Count : 0,
            Last7dVsPrior7d = new SleepTrendsDelta
            {
                ScoreDelta       = l7Score.HasValue && p7Score.HasValue ? l7Score - p7Score : null,
                TirDelta         = l7Tir.HasValue   && p7Tir.HasValue   ? l7Tir   - p7Tir   : null,
                DeepMinutesDelta = l7Deep.HasValue  && p7Deep.HasValue  ? l7Deep  - p7Deep  : null,
                DawnRiseDelta    = null,
            },
        };
    }
}
