using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Nocturne.Infrastructure.Data.Extensions;
using Nocturne.Infrastructure.Data.Interceptors;
using Nocturne.Tests.Shared.Infrastructure;
using Npgsql;

namespace Nocturne.Infrastructure.Data.Tests.Migrations;

[Trait("Category", "Integration")]
public class MigrationCeilingTests
{
    [Fact]
    public async Task A_ceiling_below_the_applied_migrations_reverts_nothing()
    {
        var database = await SharedPostgres.CreateMigratedDatabaseAsync("migration_ceiling_no_revert");
        await using var dataSource = new NpgsqlDataSourceBuilder(database.MigratorConnectionString).Build();
        var all = await AppliedAsync(dataSource);

        using (var context = DatabaseInitializationExtensions.CreateMigratorContext(
            dataSource, new TenantConnectionInterceptor(), NullLogger.Instance, ceiling: all[1]))
        {
            context.Database.GetMigrations().Should().Equal(all[0], all[1]);
            await context.Database.MigrateAsync();
        }

        (await AppliedAsync(dataSource)).Should().Equal(all,
            "another instance can apply past this one's ceiling between two of its calls");
    }

    [Fact]
    public async Task A_partly_migrated_database_finishes_across_several_calls()
    {
        var database = await SharedPostgres.CreateEmptyDatabaseAsync("migration_ceiling_resume");
        await using var dataSource = new NpgsqlDataSourceBuilder(database.MigratorConnectionString).Build();
        string[] all;
        using (var context = DatabaseInitializationExtensions.CreateMigratorContext(
            dataSource, new TenantConnectionInterceptor(), NullLogger.Instance))
        {
            all = context.Database.GetMigrations().ToArray();
            await context.GetService<IMigrator>().MigrateAsync(all[DatabaseInitializationExtensions.MigrationsPerCall / 2]);
        }

        await DatabaseInitializationExtensions.RunMigrationsAsync(
            database.MigratorConnectionString, NullLogger.Instance, new TenantConnectionInterceptor());

        (await AppliedAsync(dataSource)).Should().Equal(all);
    }

    private static async Task<List<string>> AppliedAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"");
        await using var reader = await command.ExecuteReaderAsync();
        var applied = new List<string>();
        while (await reader.ReadAsync())
        {
            applied.Add(reader.GetString(0));
        }

        return applied;
    }
}
