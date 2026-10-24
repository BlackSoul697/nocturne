using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Nocturne.Infrastructure.Data;

/// <summary>
/// Hides from a migrator context every migration whose id sorts after <see cref="Id"/>, when it
/// has one. A
/// targetless <c>MigrateAsync</c> then applies the pending migrations up to it and no further.
/// <para>
/// A run is cut this way, not by a target, because a targetless migrate never reverts.
/// <c>MigrateAsync(target)</c> runs the <c>Down</c> of every applied migration after the target,
/// and another instance can apply past it between two of this instance's calls. Here a migration
/// applied above the ceiling is only absent from the view, and EF ignores applied ids it does not
/// know.
/// </para>
/// </summary>
internal sealed class MigrationCeiling(string? id) : IDbContextOptionsExtension
{
    public string? Id { get; } = id;

    public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services) { }

    public void Validate(IDbContextOptions options) { }

    /// <summary>
    /// Every ceiling shares one internal service provider. The view reads the ceiling from the
    /// context's options, and a provider per ceiling would rebuild the model each time.
    /// </summary>
    private sealed class ExtensionInfo(IDbContextOptionsExtension extension)
        : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;

        public override string LogFragment => $"MigrationCeiling={((MigrationCeiling)Extension).Id} ";

        public override int GetServiceProviderHashCode() => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) =>
            other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }
    }
}

/// <summary>
/// The migrations of the context's assembly up to its <see cref="MigrationCeiling"/>, or all of
/// them when it has none.
/// </summary>
#pragma warning disable EF1001 // MigrationsAssembly is internal API; Migrations is its documented extension point.
internal sealed class CeilingMigrationsAssembly(
    ICurrentDbContext currentContext,
    IDbContextOptions options,
    IMigrationsIdGenerator idGenerator,
    IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
    : MigrationsAssembly(currentContext, options, idGenerator, logger)
{
    private readonly string? _ceiling = options.FindExtension<MigrationCeiling>()?.Id;
    private IReadOnlyDictionary<string, TypeInfo>? _migrations;

    public override IReadOnlyDictionary<string, TypeInfo> Migrations =>
        _migrations ??= _ceiling is null
            ? base.Migrations
            : base.Migrations
                .Where(migration => string.CompareOrdinal(migration.Key, _ceiling) <= 0)
                .ToDictionary();
}
#pragma warning restore EF1001
