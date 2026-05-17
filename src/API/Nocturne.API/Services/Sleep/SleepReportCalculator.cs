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
}
