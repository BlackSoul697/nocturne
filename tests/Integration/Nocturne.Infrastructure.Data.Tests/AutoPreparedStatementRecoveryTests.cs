using Microsoft.Extensions.DependencyInjection;
using Nocturne.Core.Contracts.Audit;
using Nocturne.Infrastructure.Data.Entities.V4;
using Nocturne.Infrastructure.Data.Extensions;
using Nocturne.Infrastructure.Data.Services;
using Nocturne.Infrastructure.Data.Tests.Rls;
using Nocturne.Infrastructure.Data.Tests.V4Goldens;
using Npgsql;

namespace Nocturne.Infrastructure.Data.Tests;

/// <summary>
/// A write PostgreSQL refuses while binding its values (a string too long for its column, 22001)
/// leaves the statement it was auto-preparing marked prepared without a row description
/// (npgsql/npgsql#6391). The next run of that statement on the same physical connection, from any
/// later lessee, then fails with "Received unexpected backend message DataRow". The pool is capped
/// at one connection and preparation starts on a statement's second use, so the refused write is
/// always the one that prepares, and every write below shares its backend. A write attributed to a
/// user batches its audit row ahead of the refused insert, as an API request's does.
/// </summary>
[Trait("Category", "Integration")]
[Collection("RLS completeness")]
public class AutoPreparedStatementRecoveryTests(RlsCompletenessFixture fx) : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private Guid _tenant;

    public async Task InitializeAsync()
    {
        await using (var seeding = TenantReaderData.BuildProvider(fx.AppConnectionString))
            _tenant = await TenantReaderData.SeedTenantAsync(fx, seeding);

        var connectionString = new NpgsqlConnectionStringBuilder(fx.AppConnectionString)
        {
            AutoPrepareMinUsages = 2,
        }.ConnectionString;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPostgreSqlInfrastructure(connectionString, configuration: null, c => c.MaxPoolSize = 1);
        services.AddScoped<TestAuditContext>();
        services.AddScoped<IAuditContext>(sp => sp.GetRequiredService<TestAuditContext>());
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_write_refused_while_preparing_does_not_break_the_next_lessees_write(bool audited)
    {
        await InsertAsync(Note("first"), audited);
        await RefusedAsync(() => InsertAsync(Note("refused", legacyId: new string('x', 300)), audited));

        await InsertAsync(Note("after"), audited);

        (await StoredTextsAsync()).Should().Contain(["first", "after"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_write_refused_while_preparing_inside_a_transaction_does_not_break_the_next_lessees_write(bool audited)
    {
        await InsertAsync(Note("first"), audited);
        await using (var scope = Scope(audited))
        await using (var db = await ContextAsync(scope))
        {
            await RefusedAsync(() => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                db.Notes.Add(Note("refused", legacyId: new string('x', 300)));
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }));
        }

        await InsertAsync(Note("after"), audited);

        (await StoredTextsAsync()).Should().Contain(["first", "after"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_write_refused_while_preparing_does_not_break_the_same_transactions_next_write(bool audited)
    {
        await InsertAsync(Note("first"), audited);
        await using (var scope = Scope(audited))
        await using (var db = await ContextAsync(scope))
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                var refused = Note("refused", legacyId: new string('x', 300));
                db.Notes.Add(refused);
                await RefusedAsync(() => db.SaveChangesAsync());
                db.Entry(refused).State = EntityState.Detached;

                db.Notes.Add(Note("after"));
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            });
        }

        (await StoredTextsAsync()).Should().Contain(["first", "after"]);
    }

    [Fact]
    public async Task A_command_in_the_aborted_transaction_leaves_the_repair_to_the_next_lessee()
    {
        await InsertAsync(Note("first"), audited: false);
        await using (var scope = Scope(audited: false))
        await using (var db = await ContextAsync(scope))
        {
            db.Database.AutoSavepointsEnabled = false;
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                db.Notes.Add(Note("refused", legacyId: new string('x', 300)));
                await RefusedAsync(() => db.SaveChangesAsync());

                var read = () => db.Notes.CountAsync();

                (await read.Should().ThrowAsync<PostgresException>())
                    .Which.SqlState.Should().Be(PostgresErrorCodes.InFailedSqlTransaction);
            });
        }

        await InsertAsync(Note("after"), audited: false);

        (await StoredTextsAsync()).Should().Contain(["first", "after"]);
    }

    private static NoteEntity Note(string text, string? legacyId = null) => new()
    {
        Id = Guid.CreateVersion7(), Timestamp = DateTime.UtcNow, Text = text, LegacyId = legacyId,
    };

    private AsyncServiceScope Scope(bool audited)
    {
        var scope = _provider.CreateAsyncScope();
        if (audited)
            scope.ServiceProvider.GetRequiredService<TestAuditContext>().AttributeToUser(Guid.NewGuid());
        return scope;
    }

    private async Task<NocturneDbContext> ContextAsync(AsyncServiceScope scope) =>
        await scope.ServiceProvider.GetRequiredService<INocturneDbContextFactory>().CreateForTenantAsync(_tenant);

    private async Task InsertAsync(NoteEntity note, bool audited)
    {
        await using var scope = Scope(audited);
        await using var db = await ContextAsync(scope);
        db.Notes.Add(note);
        await db.SaveChangesAsync();
    }

    private static async Task RefusedAsync(Func<Task> write) =>
        (await write.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.StringDataRightTruncation);

    private async Task<List<string?>> StoredTextsAsync()
    {
        await using var scope = Scope(audited: false);
        await using var db = await ContextAsync(scope);
        return await db.Notes.Select(n => n.Text).ToListAsync();
    }
}
