using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Nocturne.Infrastructure.Data.Interceptors;
using Npgsql;
using Nocturne.Tests.Shared.Infrastructure;

namespace Nocturne.Infrastructure.Data.Tests.Migrations;

/// <summary>
/// Runs <c>RemoveLegacyProfileSpansAndSwitchSnapshots</c> over the Profile spans
/// <c>AddProfileActivations</c> converted and the <c>@@@@@</c> profile rows beside them, in two tenants
/// beside rows of every kind it must leave, then down and up again, on a real PostgreSQL.
/// </summary>
public class RemoveLegacyProfileSpansAndSwitchSnapshotsFixture : IAsyncLifetime
{
    private const string MigrationUnderTest = "20261021131616_RemoveLegacyProfileSpansAndSwitchSnapshots";

    /// <summary>The last migration before profile activations, where Profile spans were still written.</summary>
    private const string BeforeActivations = "20261020015113_ConvertMotionClassActivitiesToMotionSpans";

    private const string Marker = "@@@@@";

    private string _migratorConnectionString = string.Empty;
    private string _superuserConnectionString = string.Empty;
    private string _priorMigration = string.Empty;

    internal static readonly DateTime Day = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    internal static readonly Guid TenantA = Guid.Parse("a1111111-1111-7111-8111-111111111111");
    internal static readonly Guid TenantB = Guid.Parse("b2222222-2222-7222-8222-222222222222");

    internal static readonly Guid Switch = Guid.Parse("d0000000-0000-7000-8000-000000000001");
    internal static readonly Guid Superseded = Guid.Parse("d0000000-0000-7000-8000-000000000002");
    internal static readonly Guid DeletedSwitch = Guid.Parse("d0000000-0000-7000-8000-000000000003");
    internal static readonly Guid SwitchInB = Guid.Parse("d0000000-0000-7000-8000-000000000004");
    internal static readonly Guid Override = Guid.Parse("d0000000-0000-7000-8000-000000000005");

    /// <summary>An Override that names a Profile span as its successor: no supersession writes that.</summary>
    internal static readonly Guid OverridePointingAtASwitch = Guid.Parse("d0000000-0000-7000-8000-000000000006");

    internal static readonly Guid SwitchLink = Guid.Parse("e0000000-0000-7000-8000-000000000001");
    internal static readonly Guid SupersededLink = Guid.Parse("e0000000-0000-7000-8000-000000000002");
    internal static readonly Guid OverrideLink = Guid.Parse("e0000000-0000-7000-8000-000000000003");

    /// <summary>A link of another record type that holds a Profile span's id as its record id.</summary>
    internal static readonly Guid BolusLinkSharingASwitchId = Guid.Parse("e0000000-0000-7000-8000-000000000004");

    internal static readonly string[] ProfileTables =
        ["therapy_settings", "basal_schedules", "carb_ratio_schedules", "sensitivity_schedules", "target_range_schedules"];

    internal Snapshot Seeded { get; private set; } = null!;
    internal Snapshot AfterUp { get; private set; } = null!;
    internal Snapshot AfterSecondUp { get; private set; } = null!;
    internal IReadOnlyList<Guid> ActivationsBefore { get; private set; } = [];

    /// <summary>
    /// Each row the migration's statements deleted when run as the superuser, which bypasses RLS,
    /// with the tenant the loop had pinned at the time.
    /// </summary>
    internal List<(string Table, Guid RowTenant, string Pinned)> DeletesAsSuperuser { get; } = [];

    internal sealed record Snapshot(
        Dictionary<Guid, (Guid Tenant, string Category, Guid? SupersededBy)> Spans,
        Dictionary<Guid, (Guid Tenant, string RecordType, Guid RecordId)> Links,
        List<(string Table, Guid Tenant, string ProfileName)> ProfileRows,
        Dictionary<Guid, (Guid Tenant, DateTime? DeletedAt)> Activations);

    public async Task InitializeAsync()
    {
        var database = await SharedPostgres.CreateEmptyDatabaseAsync("remove_legacy_profile_spans");
        _migratorConnectionString = database.MigratorConnectionString;
        _superuserConnectionString = database.SuperuserConnectionString;
        _priorMigration = await PriorMigrationAsync();

        await MigrateToAsync(BeforeActivations);
        await SeedSpansAsync();
        await MigrateToAsync(_priorMigration);
        await SeedLinksAndProfileRowsAsync();
        Seeded = await ReadAsync();
        await MigrateToAsync(targetMigration: null);
        AfterUp = await ReadAsync();
        await MigrateToAsync(_priorMigration);
        await MigrateToAsync(targetMigration: null);
        AfterSecondUp = await ReadAsync();
        await RunAsSuperuserAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private DbContextOptions<NocturneDbContext> Options(bool interceptor = true)
    {
        var builder = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseNpgsql(_migratorConnectionString, npgsql => npgsql.UseNocturneMigrations());
        if (interceptor)
            builder.AddInterceptors(new TenantConnectionInterceptor());
        return builder.Options;
    }

    private async Task<string> PriorMigrationAsync()
    {
        await using var context = new NocturneDbContext(Options(interceptor: false));
        var migrations = context.Database.GetMigrations().ToList();
        return migrations[migrations.IndexOf(MigrationUnderTest) - 1];
    }

    private async Task MigrateToAsync(string? targetMigration)
    {
        await using var context = new NocturneDbContext(Options());
        await context.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    private async Task<string> UpSqlAsync()
    {
        await using var context = new NocturneDbContext(Options(interceptor: false));
        var assembly = context.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(
            assembly.Migrations[MigrationUnderTest], context.Database.ProviderName!);
        return migration.UpOperations.OfType<SqlOperation>().Single().Sql;
    }

    private static async Task ExecuteAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static string SpanValues(Guid id, Guid tenant, string category, string originalId, DateTime start,
        Guid? supersededBy = null, bool deleted = false) =>
        $$"""
        ('{{id}}', '{{tenant}}', '{{category}}', 'Active', '{{start:O}}', '{{originalId}}', 'careportal',
         '{"profileName":"Weekend","utcOffset":0}', {{(supersededBy is { } s ? $"'{s}'" : "NULL")}},
         now(), now(), {{(deleted ? "now()" : "NULL")}}, {{(deleted ? "true" : "false")}})
        """;

    private const string SpanColumns = """
        INSERT INTO state_spans
          (id, tenant_id, category, state, start_timestamp, original_id, source, metadata, superseded_by_id,
           created_at, updated_at, deleted_at, deleted_by_user)
        VALUES
        """;

    private async Task SeedSpansAsync()
    {
        await using var conn = new NpgsqlConnection(_migratorConnectionString);
        await conn.OpenAsync();
        await ExecuteAsync(conn, $"""
            INSERT INTO tenants (id, slug, display_name, is_active, sys_created_at, sys_updated_at) VALUES
              ('{TenantA}', 'tenant-a', 'Tenant A', true, now(), now()),
              ('{TenantB}', 'tenant-b', 'Tenant B', true, now(), now());

            SELECT set_config('app.current_tenant_id', '{TenantA}', false);
            {SpanColumns}
              {SpanValues(Switch, TenantA, "Profile", "ps-1", Day.AddHours(2))},
              {SpanValues(Superseded, TenantA, "Profile", "ps-0", Day.AddHours(1), supersededBy: Switch)},
              {SpanValues(DeletedSwitch, TenantA, "Profile", "ps-x", Day.AddHours(3), deleted: true)},
              {SpanValues(Override, TenantA, "Override", "ov-1", Day.AddHours(4))},
              {SpanValues(OverridePointingAtASwitch, TenantA, "Override", "ov-2", Day.AddHours(5), supersededBy: Switch)};

            SELECT set_config('app.current_tenant_id', '{TenantB}', false);
            {SpanColumns}
              {SpanValues(SwitchInB, TenantB, "Profile", "ps-b", Day.AddHours(2))};

            SELECT set_config('app.current_tenant_id', '', false);
            """);
    }

    private static string ProfileRowsSql(Guid tenant, string profileName, string legacyId)
    {
        var settings = $"""
            INSERT INTO therapy_settings
              (id, tenant_id, timestamp, legacy_id, profile_name, dia, carbs_hr, delay, is_default, is_externally_managed)
            VALUES (gen_random_uuid(), '{tenant}', '{Day:O}', '{legacyId}', '{profileName}', 4, 20, 20, false, false);
            """;
        var schedules = string.Concat(ProfileTables.Skip(1).Select(table => $"""
            INSERT INTO {table} (id, tenant_id, timestamp, legacy_id, profile_name, entries_json)
            VALUES (gen_random_uuid(), '{tenant}', '{Day:O}', '{legacyId}', '{profileName}', '[]');
            """));
        return settings + schedules;
    }

    private async Task SeedLinksAndProfileRowsAsync()
    {
        await using var conn = new NpgsqlConnection(_migratorConnectionString);
        await conn.OpenAsync();
        var canonical = Guid.CreateVersion7();
        await ExecuteAsync(conn, $"""
            SELECT set_config('app.current_tenant_id', '{TenantA}', false);
            INSERT INTO linked_records (id, tenant_id, canonical_id, record_type, record_id, source_timestamp, data_source, is_primary) VALUES
              ('{SwitchLink}', '{TenantA}', '{canonical}', 'statespan', '{Switch}', 0, 'careportal', true),
              ('{SupersededLink}', '{TenantA}', '{canonical}', 'statespan', '{Superseded}', 0, 'xdrip', false),
              ('{OverrideLink}', '{TenantA}', '{Guid.CreateVersion7()}', 'statespan', '{Override}', 0, 'loop', true),
              ('{BolusLinkSharingASwitchId}', '{TenantA}', '{Guid.CreateVersion7()}', 'bolus', '{Switch}', 0, 'loop', true);
            {ProfileRowsSql(TenantA, $"Weekend{Marker}1780272000000", $"ps-1:Weekend{Marker}1780272000000")}
            {ProfileRowsSql(TenantA, $"Sick{Marker}1780275600000", $"ps-x:Sick{Marker}1780275600000")}
            UPDATE therapy_settings SET deleted_at = now() WHERE profile_name = 'Sick{Marker}1780275600000';
            {ProfileRowsSql(TenantA, "Weekend", "doc-a:Weekend")}
            {ProfileRowsSql(TenantA, "Four@@@@", "doc-a:Four@@@@")}

            SELECT set_config('app.current_tenant_id', '{TenantB}', false);
            {ProfileRowsSql(TenantB, $"Other{Marker}1780272000000", $"ps-b:Other{Marker}1780272000000")}
            {ProfileRowsSql(TenantB, "Other", "doc-b:Other")}

            SELECT set_config('app.current_tenant_id', '', false);
            """);
    }

    private async Task<Snapshot> ReadAsync()
    {
        await using var conn = new NpgsqlConnection(_superuserConnectionString);
        await conn.OpenAsync();
        var snapshot = new Snapshot([], [], [], []);

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, tenant_id, category, superseded_by_id FROM state_spans;";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                snapshot.Spans[reader.GetGuid(0)] =
                    (reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3));
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, tenant_id, record_type, record_id FROM linked_records;";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                snapshot.Links[reader.GetGuid(0)] = (reader.GetGuid(1), reader.GetString(2), reader.GetGuid(3));
        }

        foreach (var table in ProfileTables)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT tenant_id, profile_name FROM {table};";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                snapshot.ProfileRows.Add((table, reader.GetGuid(0), reader.GetString(1)));
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, tenant_id, deleted_at FROM profile_activations;";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                snapshot.Activations[reader.GetGuid(0)] =
                    (reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetDateTime(2));
        }

        return snapshot;
    }

    /// <summary>
    /// Puts a Profile span and an <c>@@@@@</c> settings row back in each tenant and reruns the
    /// statements as the superuser, logging each delete from a trigger, all in a transaction that is
    /// rolled back.
    /// </summary>
    private async Task RunAsSuperuserAsync()
    {
        var upSql = await UpSqlAsync();

        await using var conn = new NpgsqlConnection(_superuserConnectionString);
        await conn.OpenAsync();
        await using var transaction = await conn.BeginTransactionAsync();
        await ExecuteAsync(conn, $$"""
            CREATE TEMP TABLE row_deletes (tbl text, row_tenant uuid, pinned text);
            CREATE FUNCTION log_row_delete() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                INSERT INTO row_deletes VALUES (TG_TABLE_NAME, OLD.tenant_id, current_setting('app.current_tenant_id', true));
                RETURN OLD;
            END $$;
            {{SpanColumns}}
              {{SpanValues(Guid.CreateVersion7(), TenantA, "Profile", "again-a", Day)}},
              {{SpanValues(Guid.CreateVersion7(), TenantB, "Profile", "again-b", Day)}};
            {{ProfileRowsSql(TenantA, $"Again{Marker}1", $"again-a:Again{Marker}1")}}
            {{ProfileRowsSql(TenantB, $"Again{Marker}1", $"again-b:Again{Marker}1")}}
            CREATE TRIGGER log_span_delete AFTER DELETE ON state_spans
                FOR EACH ROW EXECUTE FUNCTION log_row_delete();
            CREATE TRIGGER log_settings_delete AFTER DELETE ON therapy_settings
                FOR EACH ROW EXECUTE FUNCTION log_row_delete();
            """);
        await ExecuteAsync(conn, upSql);

        await using (var read = conn.CreateCommand())
        {
            read.CommandText = "SELECT tbl, row_tenant, pinned FROM row_deletes;";
            await using var reader = await read.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                DeletesAsSuperuser.Add((reader.GetString(0), reader.GetGuid(1), reader.GetString(2)));
        }

        await transaction.RollbackAsync();
    }
}

/// <inheritdoc cref="RemoveLegacyProfileSpansAndSwitchSnapshotsFixture"/>
[Trait("Category", "Integration")]
public class RemoveLegacyProfileSpansAndSwitchSnapshotsTests(RemoveLegacyProfileSpansAndSwitchSnapshotsFixture fixture)
    : IClassFixture<RemoveLegacyProfileSpansAndSwitchSnapshotsFixture>
{
    private const string Marker = "@@@@@";

    [Fact]
    public void The_seed_holds_what_the_cutover_left_in_every_tenant()
    {
        fixture.Seeded.Spans.Values.Count(s => s.Category == "Profile").Should().Be(4);
        fixture.Seeded.ProfileRows.Count(r => r.ProfileName.Contains(Marker)).Should().Be(15);
        fixture.Seeded.Activations.Keys.Should().BeEquivalentTo(new[]
        {
            RemoveLegacyProfileSpansAndSwitchSnapshotsFixture.Switch,
            RemoveLegacyProfileSpansAndSwitchSnapshotsFixture.Superseded,
            RemoveLegacyProfileSpansAndSwitchSnapshotsFixture.DeletedSwitch,
            RemoveLegacyProfileSpansAndSwitchSnapshotsFixture.SwitchInB,
        }, "each Profile span was converted under its own id");
    }

    [Fact]
    public void Every_profile_span_in_every_tenant_is_deleted_outright_and_no_other_span_is()
    {
        fixture.AfterUp.Spans.Values.Should().NotContain(s => s.Category == "Profile");
        fixture.AfterUp.Spans.Keys.Should().BeEquivalentTo(new[]
        {
            RemoveLegacyProfileSpansAndSwitchSnapshotsFixture.Override,
            RemoveLegacyProfileSpansAndSwitchSnapshotsFixture.OverridePointingAtASwitch,
        });
    }

    [Fact]
    public void A_span_that_named_a_profile_span_as_its_successor_survives_without_the_pointer()
    {
        fixture.AfterUp.Spans[RemoveLegacyProfileSpansAndSwitchSnapshotsFixture.OverridePointingAtASwitch]
            .SupersededBy.Should().BeNull();
    }

    [Fact]
    public void Only_the_profile_spans_state_span_links_go()
    {
        fixture.AfterUp.Links.Keys.Should().BeEquivalentTo(new[]
        {
            RemoveLegacyProfileSpansAndSwitchSnapshotsFixture.OverrideLink,
            RemoveLegacyProfileSpansAndSwitchSnapshotsFixture.BolusLinkSharingASwitchId,
        });
    }

    [Fact]
    public void Every_switch_snapshot_row_goes_live_or_deleted_and_every_profile_document_row_stays()
    {
        fixture.AfterUp.ProfileRows.Should().NotContain(r => r.ProfileName.Contains(Marker));
        fixture.AfterUp.ProfileRows.Should().BeEquivalentTo(
            fixture.Seeded.ProfileRows.Where(r => !r.ProfileName.Contains(Marker)));
        fixture.AfterUp.ProfileRows.Should().Contain(r => r.ProfileName == "Four@@@@");
        fixture.AfterUp.ProfileRows.Select(r => r.Tenant).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void The_activations_are_untouched()
    {
        fixture.AfterUp.Activations.Should().BeEquivalentTo(fixture.Seeded.Activations);
    }

    [Fact]
    public void Down_restores_nothing_and_up_again_changes_nothing()
    {
        fixture.AfterSecondUp.Should().BeEquivalentTo(fixture.AfterUp);
    }

    [Fact]
    public void Run_by_a_role_that_bypasses_rls_each_tenants_rows_are_deleted_with_that_tenant_pinned()
    {
        fixture.DeletesAsSuperuser.Should().HaveCount(4);
        fixture.DeletesAsSuperuser.Should().OnlyContain(d => d.Pinned == d.RowTenant.ToString());
        fixture.DeletesAsSuperuser.Select(d => d.RowTenant).Distinct().Should().HaveCount(2);
    }
}
