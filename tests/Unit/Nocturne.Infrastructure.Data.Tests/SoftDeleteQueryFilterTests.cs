using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.Infrastructure.Data.Tests;

[Trait("Category", "Unit")]
public class SoftDeleteQueryFilterTests : IDisposable
{
    private readonly DbConnection _connection;
    private readonly DbContextOptions<NocturneDbContext> _contextOptions;
    private readonly Guid _tenantId = Guid.CreateVersion7();

    public SoftDeleteQueryFilterTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _contextOptions = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;
        using var context = new NocturneDbContext(_contextOptions);
        context.Database.EnsureCreated();

        // Seed the tenant so FK constraints on TenantId are satisfied
        context.Tenants.Add(new TenantEntity { Id = _tenantId, Slug = "test" });
        context.SaveChanges();
    }

    private NocturneDbContext CreateContext()
    {
        var context = new NocturneDbContext(_contextOptions);
        context.TenantId = _tenantId;
        return context;
    }

    [Fact]
    public async Task GetTreatments_ExcludesSoftDeletedRecords()
    {
        using var context = CreateContext();
        var activeTreatment = new TreatmentEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 1000,
            TenantId = _tenantId,
            DeletedAt = null,
        };
        var deletedTreatment = new TreatmentEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 2000,
            TenantId = _tenantId,
            DeletedAt = DateTime.UtcNow,
        };
        context.Treatments.AddRange(activeTreatment, deletedTreatment);
        await context.SaveChangesAsync();

        var results = await context.Treatments.ToListAsync();

        Assert.Single(results);
        Assert.Equal(activeTreatment.Id, results[0].Id);
    }

    [Fact]
    public void TreatmentMapper_ToDomainModel_SetsIsValidFalse_WhenDeletedAtIsSet()
    {
        var entity = new TreatmentEntity
        {
            Id = Guid.CreateVersion7(),
            Mills = 1000,
            DeletedAt = DateTime.UtcNow,
        };
        entity.Aaps.IsValid = true;

        var model = TreatmentMapper.ToDomainModel(entity);

        Assert.False(model.IsValid);
    }

    public void Dispose() => _connection.Dispose();
}
