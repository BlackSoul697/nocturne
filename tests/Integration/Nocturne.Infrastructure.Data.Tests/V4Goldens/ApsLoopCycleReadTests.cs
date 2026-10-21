using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nocturne.Core.Contracts.Audit;
using Nocturne.Core.Contracts.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Repositories.V4;
using Nocturne.Tests.Shared.Infrastructure;
using Npgsql;

namespace Nocturne.Infrastructure.Data.Tests.V4Goldens;

[Trait("Category", "Integration")]
[Collection("V4 goldens")]
public class ApsLoopCycleReadTests(V4GoldenFixture fixture)
{
    private static readonly DateTime Start = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string CappedReason =
        "COB: 0, Dev: 12, BGI: -1, ISF: 50, CR: 10, Target: 100; Eventual BG 180 >= 100, insulinReq 0.9. "
        + "Microbolusing 0.4U. adj. req. rate: 3.2 to maxSafeBasal: 2.4, temp 1.2 < 2.4U/hr.";

    [Fact]
    public async Task Reads_each_cycle_and_the_limits_its_suggestion_names()
    {
        var tenant = Guid.NewGuid();
        using var scope = await fixture.BeginTenantScopeAsync(tenant);
        var repo = scope.ServiceProvider.GetRequiredService<IApsSnapshotRepository>();
        await SeedAsync(repo);

        var cycles = await repo.GetLoopCyclesAsync(Start, Start.AddMinutes(20));

        cycles.Should().HaveCount(4);
        var withLimits = cycles[0];
        withLimits.AidAlgorithm.Should().Be(AidAlgorithm.AndroidAps);
        withLimits.Enacted.Should().BeTrue();
        withLimits.EnactedRate.Should().Be(2.4);
        withLimits.EnactedDuration.Should().Be(30);
        withLimits.Iob.Should().Be(1.5);
        withLimits.SensitivityRatio.Should().Be(1.1);
        withLimits.HasSuggestion.Should().BeTrue();
        withLimits.HasReason.Should().BeTrue();
        withLimits.SuggestedChange.Should().BeTrue();
        withLimits.LimitReason.Should().Be(CappedReason);
        withLimits.MaxIob.Should().Be(3.5);
        withLimits.MaxBasal.Should().Be(2.4);
        withLimits.MaxDailySafetyMultiplier.Should().Be(3);
        withLimits.CurrentBasalSafetyMultiplier.Should().Be(4);

        var withoutLimits = cycles[1];
        withoutLimits.HasSuggestion.Should().BeTrue();
        withoutLimits.SuggestedChange.Should().BeFalse("it asked for neither a temp nor a microbolus");
        withoutLimits.LimitReason.Should().BeNull("its reason names no limit");
        withoutLimits.MaxIob.Should().BeNull("a key the suggestion omits is unknown, not a default");
        withoutLimits.MaxBasal.Should().BeNull();
        withoutLimits.MaxDailySafetyMultiplier.Should().BeNull();
        withoutLimits.CurrentBasalSafetyMultiplier.Should().BeNull();

        cycles[2].MaxIob.Should().BeNull("a limit that is not a number is unknown");

        var loop = cycles[3];
        loop.AidAlgorithm.Should().Be(AidAlgorithm.Loop);
        loop.HasSuggestion.Should().BeFalse();
        loop.HasReason.Should().BeFalse();
        loop.SuggestedChange.Should().BeNull();
        loop.EnactedBolusVolume.Should().Be(0.3);
    }

    [Fact]
    public async Task Reads_no_blob_column_and_only_the_named_members_of_the_suggestion()
    {
        var tenant = Guid.NewGuid();
        using (var scope = await fixture.BeginTenantScopeAsync(tenant))
            await SeedAsync(scope.ServiceProvider.GetRequiredService<IApsSnapshotRepository>());

        await using var conn = new NpgsqlConnection(fixture.AppConnectionString);
        await conn.OpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tid, false), set_config('app.is_share', 'false', false)";
            cmd.Parameters.AddWithValue("tid", tenant.ToString());
            await cmd.ExecuteNonQueryAsync();
        }
        var statements = new StatementRecorder();
        await using var ctx = new NocturneDbContext(new DbContextOptionsBuilder<NocturneDbContext>()
            .UseNpgsql(conn)
            .AddInterceptors(statements)
            .Options) { TenantId = tenant };
        var repo = new ApsSnapshotRepository(
            new TestNocturneDbContextFactory(ctx), new SystemAuditContext(), NullLogger<ApsSnapshotRepository>.Instance);

        (await repo.GetLoopCyclesAsync(Start, Start.AddMinutes(20))).Should().HaveCount(4);

        var select = statements.All.Should().ContainSingle(s => s.StartsWith("SELECT")).Subject;
        foreach (var blob in new[]
                 {
                     "enacted_json", "predicted_default_json", "predicted_iob_json", "predicted_zt_json",
                     "predicted_cob_json", "predicted_uam_json", "loop_json", "additional_properties",
                 })
            select.Should().NotContain(blob);
        System.Text.RegularExpressions.Regex.Replace(select, @"jsonb_extract_path_text\(a\.suggested_json, '\w+'\)", "")
            .Replace("a.suggested_json IS NULL", "")
            .Replace("a.suggested_json IS NOT NULL", "")
            .Should().NotContain("suggested_json", "the suggestion is read only through the members it names");
    }

    private static async Task SeedAsync(IApsSnapshotRepository repo)
    {
        var curve = JsonSerializer.Serialize(Enumerable.Range(0, 48).Select(i => 100 + i).ToArray());
        await repo.BulkCreateAsync(
        [
            new ApsSnapshot
            {
                Timestamp = Start, AidAlgorithm = AidAlgorithm.AndroidAps, Enacted = true, EnactedRate = 2.4,
                EnactedDuration = 30, Iob = 1.5, SensitivityRatio = 1.1, DataSource = "loop-cycle-test",
                SuggestedJson = JsonSerializer.Serialize(new
                {
                    reason = CappedReason, rate = 2.4, duration = 30, units = 0.4,
                    max_iob = 3.5, max_basal = 2.4, max_daily_safety_multiplier = 3, current_basal_safety_multiplier = 4,
                    predBGs = new { IOB = Enumerable.Range(0, 48).ToArray() },
                }),
                EnactedJson = "{\"received\":true,\"rate\":2.4}",
                PredictedIobJson = curve, PredictedZtJson = curve, PredictedCobJson = curve, PredictedUamJson = curve,
            },
            new ApsSnapshot
            {
                Timestamp = Start.AddMinutes(5), AidAlgorithm = AidAlgorithm.AndroidAps, Enacted = false,
                DataSource = "loop-cycle-test",
                SuggestedJson = "{\"reason\":\"Eventual BG 110 > 100, temp 1.0 ~ req 1.0U/hr\",\"bg\":110}",
            },
            new ApsSnapshot
            {
                Timestamp = Start.AddMinutes(10), AidAlgorithm = AidAlgorithm.AndroidAps, Enacted = true,
                DataSource = "loop-cycle-test",
                SuggestedJson = "{\"reason\":\"\",\"duration\":30,\"max_iob\":\"lots\"}",
            },
            new ApsSnapshot
            {
                Timestamp = Start.AddMinutes(15), AidAlgorithm = AidAlgorithm.Loop, Enacted = true, EnactedRate = 0,
                EnactedBolusVolume = 0.3, DataSource = "loop-cycle-test", LoopJson = "{\"enacted\":{\"received\":true}}",
                PredictedDefaultJson = curve,
            },
        ], WriteOrigin.Backfill);
    }
}
