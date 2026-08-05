using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Storage.Internal;
using Nocturne.Infrastructure.Data;

namespace Nocturne.Tests.Shared.Infrastructure;

public static class TestDbContextFactory
{
    /// <summary>
    /// Creates an in-memory context, optionally pinned to <paramref name="tenantId"/>.
    /// </summary>
    /// <param name="databaseName">Store name; contexts sharing one see each other's rows.</param>
    /// <param name="tenantId">
    /// The tenant to pin, standing in for what <c>TenantResolutionMiddleware</c> or a pinned factory
    /// would set in production. Tenant-scoped entities carry a query filter on this, so a fixture
    /// that seeds rows for a tenant and reads them back on an unpinned context sees nothing. Left
    /// unpinned by default, which is the tenantless-entry-point state.
    /// </param>
    public static NocturneDbContext CreateInMemoryContext(string? databaseName = null, Guid tenantId = default)
    {
        var options = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"nocturne_tests_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .EnableSensitiveDataLogging()
            .Options;

        var context = new NocturneDbContext(options) { TenantId = tenantId };
        context.Database.EnsureCreated();
        return context;
    }
}
