using Nocturne.Core.Clinical.LoopAnalysis;
using Nocturne.Core.Clinical.Treatments;
using Nocturne.Core.Models.Basal;
using Nocturne.Core.Models.LoopAnalysis;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Clinical.Tests.LoopAnalysis;

/// <summary>A meal: announced ones are logged and bolused at the profile's carb ratio, hidden ones neither.</summary>
public readonly record struct SimulatedMeal(long Mills, double Grams, bool Announced = true);

/// <summary>
/// A closed loop run in five-minute steps against a body with known true settings, while the profile
/// the loop and the calculator read may be wrong. Insulin acts on a triangular curve over four hours
/// (peak at 75 minutes), which is also the curve the loop and the reported IOB use, so IOB is exact;
/// carbs absorb evenly over three hours. A steady basal of the true rate holds glucose flat.
/// </summary>
public sealed class LoopSimulation
{
    public const long StepMs = 5 * LocalSlotBinning.MinuteMs;
    private const int DiaSteps = 48;
    private const int PeakSteps = 15;
    private const int AbsorptionSteps = 36;
    private const double StepHours = 5.0 / 60;

    private static readonly double[] Activity = BuildActivity();

    /// <summary>The share of a dose still to act <c>k</c> steps after it.</summary>
    private static readonly double[] RemainingAfter = [.. Enumerable.Range(0, DiaSteps).Select(k => 1 - Activity.Take(k).Sum())];

    public required TimeZoneInfo Zone { get; init; }
    public double TrueBasal { get; init; } = 1.0;
    public double TrueIsf { get; init; } = 50;
    public double TrueCr { get; init; } = 10;
    public double ProfileBasal { get; init; } = 1.0;
    public double ProfileIsf { get; init; } = 50;
    public double ProfileCr { get; init; } = 10;

    /// <summary>Automatic boluses for half of what the loop wants, as oref's SMB does; else temp basals alone.</summary>
    public bool Smb { get; init; }

    public double LoopTarget { get; init; } = 110;
    public (double Low, double High) TargetRange { get; init; } = (90, 130);

    /// <summary>Uniform noise on each reading, ± this, mg/dL, from a fixed seed.</summary>
    public double NoiseMgdl { get; init; } = 2;

    public IReadOnlyList<SimulatedMeal> Meals { get; init; } = [];
    public IReadOnlyList<(long Mills, double Units)> Corrections { get; init; } = [];

    public List<GlucoseSample> Glucose { get; } = [];
    public List<CarbEvent> Carbs { get; } = [];
    public List<InsulinDose> Doses { get; } = [];
    public List<DeliveredBasalSegment> Delivered { get; } = [];
    public List<IobCobTick> IobCob { get; } = [];

    /// <summary>Runs from <paramref name="from"/> to <paramref name="to"/>, starting at the loop's target in steady state.</summary>
    public LoopSimulation Run(long from, long to)
    {
        var steps = (int)((to - from) / StepMs);
        var absolute = new double[steps + 1];
        var scheduled = new double[steps + 1];
        var carbEffect = new double[steps + AbsorptionSteps + 1];
        var cobGrams = new double[steps + AbsorptionSteps + 1];
        var random = new Random(2392);
        var glucose = LoopTarget;

        var meals = Meals.ToLookup(m => (int)((m.Mills - from) / StepMs));
        var corrections = Corrections.ToLookup(c => (int)((c.Mills - from) / StepMs), c => c.Units);
        // Steady state before the run: every step of the curve already carries a step's scheduled basal.
        var history = TrueBasal * StepHours;
        absolute[0] = history;
        scheduled[0] = ProfileBasal * StepHours;

        for (var j = 0; j < steps; j++)
        {
            var t = from + j * StepMs;
            foreach (var meal in meals[j])
            {
                for (var k = 0; k < AbsorptionSteps; k++)
                {
                    carbEffect[j + k] += TrueIsf / TrueCr * meal.Grams / AbsorptionSteps;
                    if (meal.Announced)
                        cobGrams[j + k] += meal.Grams * (AbsorptionSteps - k) / AbsorptionSteps;
                }
                if (meal.Announced)
                {
                    Carbs.Add(new CarbEvent(t, meal.Grams));
                    Bolus(j, t, meal.Grams / ProfileCr, automatic: false, absolute);
                }
            }
            foreach (var units in corrections[j])
                Bolus(j, t, units, automatic: false, absolute);

            var iob = Remaining(absolute, j) - Remaining(scheduled, j)
                + (TrueBasal - ProfileBasal) * StepHours * RemainingBefore(j);
            var eventual = glucose - ProfileIsf * iob + ProfileIsf / ProfileCr * cobGrams[j];
            var required = (eventual - LoopTarget) / ProfileIsf;
            var rate = Math.Clamp(ProfileBasal + 2 * required, 0, 4 * ProfileBasal);
            if (Smb && required > 0)
            {
                var smb = Math.Floor(Math.Min(0.5 * required, 1) / 0.05) * 0.05;
                if (smb > 0)
                {
                    Bolus(j, t, smb, automatic: true, absolute);
                    iob += smb;
                }
                rate = ProfileBasal;
            }

            Glucose.Add(new GlucoseSample(t, Math.Round(glucose + (random.NextDouble() * 2 - 1) * NoiseMgdl, 1)));
            IobCob.Add(new IobCobTick(t, iob, IobCobSource.Device, cobGrams[j], IobCobSource.Device));
            Delivered.Add(new DeliveredBasalSegment(t, t + StepMs, rate, BasalDeliveryOrigin.Algorithm));
            absolute[j + 1] += rate * StepHours;
            scheduled[j + 1] += ProfileBasal * StepHours;

            var acting = history * HistoryActivity(j);
            for (var i = Math.Max(0, j - DiaSteps + 1); i <= j; i++)
                acting += absolute[i] * Activity[j - i];
            glucose += TrueIsf * (TrueBasal * StepHours - acting) + carbEffect[j];
        }
        return this;
    }

    public List<RateSegment> Schedule(long from, long to) => [new RateSegment(from, to, ProfileBasal)];

    public TherapyTimeline Therapy(long from, long to) => new([new TherapySegment(from, to, new TherapySnapshot(
        dia: DiaSteps * StepHours,
        peakMinutes: PeakSteps * 5,
        carbsPerHour: 30,
        timezone: Zone,
        ccpPercentage: null,
        ccpTimeshiftMs: 0,
        sensitivityEntries: [new ScheduleEntry { TimeAsSeconds = 0, Value = ProfileIsf }],
        carbRatioEntries: [new ScheduleEntry { TimeAsSeconds = 0, Value = ProfileCr }],
        basalEntries: [new ScheduleEntry { TimeAsSeconds = 0, Value = ProfileBasal }]))]);

    private void Bolus(int step, long mills, double units, bool automatic, double[] absolute)
    {
        absolute[step] += units;
        Doses.Add(new InsulinDose(mills, units, automatic));
    }

    /// <summary>Insulin of <paramref name="doses"/> still to act at step <paramref name="j"/>, doses at it included.</summary>
    private static double Remaining(double[] doses, int j)
    {
        double sum = 0;
        for (var i = Math.Max(0, j - DiaSteps + 1); i <= j; i++)
            sum += doses[i] * RemainingAfter[j - i];
        return sum;
    }

    /// <summary>What the pre-run steady basal, one step's worth per step, still has to act at step <paramref name="j"/>.</summary>
    private static double RemainingBefore(int j)
    {
        double sum = 0;
        for (var age = j + 1; age < DiaSteps; age++)
            sum += RemainingAfter[age];
        return sum;
    }

    /// <summary>Of the pre-run steady basal, the share of one step's dose acting in step <paramref name="j"/>.</summary>
    private static double HistoryActivity(int j)
    {
        double sum = 0;
        for (var age = j + 1; age < DiaSteps; age++)
            sum += Activity[age];
        return sum;
    }

    private static double[] BuildActivity()
    {
        var weights = new double[DiaSteps];
        for (var k = 0; k < DiaSteps; k++)
            weights[k] = k < PeakSteps ? (k + 0.5) / PeakSteps : (DiaSteps - k - 0.5) / (DiaSteps - PeakSteps);
        var total = weights.Sum();
        return [.. weights.Select(w => w / total)];
    }
}
