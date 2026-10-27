using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nocturne.Core.Contracts.Audit;
using Nocturne.Core.Contracts.Infrastructure;
using Nocturne.Infrastructure.Data.Repositories;
using Nocturne.Tests.Shared.Infrastructure;
using Npgsql;

namespace Nocturne.Infrastructure.Data.Tests.Rls;

/// <summary>
/// The latest-by-source reads probe <c>ix_state_spans_tenant_source_category_start</c> once per
/// category. The query the repository actually sends is captured and explained as
/// <c>nocturne_app</c>, over enough rows that a scan would be the planner's choice without the index.
/// </summary>
[Trait("Category", "Integration")]
[Collection("RLS completeness")]
public class StateSpanLatestStartPlanTests(RlsCompletenessFixture fx)
{
    private const int Rows = 20_000;
    private const string Source = "aaps";
    private const string Index = "ix_state_spans_tenant_source_category_start";

    [Fact]
    public Task Activity_ProbesTheIndexOncePerCategory() =>
        AssertProbesAsync(
            (repository, source) => repository.GetLatestActivityTimestampAsync(source),
            arms: 4);

    [Fact]
    public Task NonActivity_ProbesTheIndexOncePerCategory() =>
        AssertProbesAsync(
            (repository, source) => repository.GetLatestNonActivityTimestampAsync(source),
            arms: 8);

    private async Task AssertProbesAsync(
        Func<StateSpanRepository, string, Task<DateTime?>> read, int arms)
    {
        var tenant = Guid.NewGuid();
        await SeedAsync(tenant);

        await using var conn = await fx.OpenAppConnectionAsync();
        await SetTenantAsync(conn, tenant);

        var capture = new LastReaderCapture();
        await using var ctx = new NocturneDbContext(new DbContextOptionsBuilder<NocturneDbContext>()
            .UseNpgsql(conn)
            .AddInterceptors(capture)
            .Options) { TenantId = tenant };

        var repository = new StateSpanRepository(
            new TestNocturneDbContextFactory(ctx),
            new Mock<IDeduplicationService>().Object,
            new Mock<IAuditContext>().Object,
            NullLogger<StateSpanRepository>.Instance);
        (await read(repository, Source)).Should().NotBeNull();

        var plan = await ExplainAsync(conn, capture.Command!);
        plan.Descendants("Node Type").Should().NotContain("Seq Scan", plan.ToString());
        plan.Descendants("Index Name").Should().Equal(
            Enumerable.Repeat(Index, arms),
            $"each category is its own probe of the tenant, source, category index; plan: {plan}");
    }

    private async Task SeedAsync(Guid tenant)
    {
        await using var conn = await fx.OpenMigratorConnectionAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO tenants (id, slug, display_name, is_active, sys_created_at, sys_updated_at)
                VALUES (@tid, @slug, 'state-span-latest-plan-test', true, now(), now())
                """;
            cmd.Parameters.AddWithValue("tid", tenant);
            cmd.Parameters.AddWithValue("slug", $"latest-{tenant:N}");
            await cmd.ExecuteNonQueryAsync();
        }

        await SetTenantAsync(conn, tenant);

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                INSERT INTO state_spans (id, tenant_id, category, state, start_timestamp, source, created_at, updated_at)
                SELECT gen_random_uuid(), @tid,
                       (ARRAY['PumpMode','PumpConnectivity','Override','Profile','Exercise','Illness','Travel','DataExclusion','TemporaryTarget','Motion','TempBasal','BasalDelivery'])[1 + g % 12], 's',
                       now() - make_interval(mins => g), '{Source}', now(), now()
                FROM generate_series(1, {Rows}) g
                """;
            cmd.Parameters.AddWithValue("tid", tenant);
            await cmd.ExecuteNonQueryAsync();
        }

        await using var analyze = conn.CreateCommand();
        analyze.CommandText = "ANALYZE state_spans";
        await analyze.ExecuteNonQueryAsync();
    }

    private static async Task SetTenantAsync(NpgsqlConnection conn, Guid tenant)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tid, false), set_config('app.is_share', 'false', false)";
        cmd.Parameters.AddWithValue("tid", tenant.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<JsonElement> ExplainAsync(NpgsqlConnection conn, NpgsqlCommand captured)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "EXPLAIN (FORMAT JSON) " + captured.CommandText;
        foreach (NpgsqlParameter p in captured.Parameters)
            cmd.Parameters.Add(p.Clone());

        var json = (string)(await cmd.ExecuteScalarAsync())!;
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private sealed class LastReaderCapture : DbCommandInterceptor
    {
        public NpgsqlCommand? Command { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            var copy = new NpgsqlCommand(command.CommandText);
            foreach (NpgsqlParameter p in command.Parameters)
                copy.Parameters.Add(p.Clone());
            Command = copy;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
