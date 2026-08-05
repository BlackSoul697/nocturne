using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nocturne.Core.Models.Authorization;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Extensions;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Ensures at least one platform admin exists on startup.
/// </summary>
/// <remarks>
/// Priority order:
/// <list type="number">
///   <item>If <c>Platform:AdminSubjectIds</c> is configured, those subjects are granted platform admin status.</item>
///   <item>Otherwise, if no platform admin exists, the owner of the oldest tenant is granted it.</item>
/// </list>
/// </remarks>
public class PlatformAdminBootstrapService
{
    private readonly NocturneDbContext _db;
    private readonly PlatformOptions _options;

    /// <summary>
    /// Initialises a new <see cref="PlatformAdminBootstrapService"/>.
    /// </summary>
    /// <param name="db">Database context for subject and tenant member queries.</param>
    /// <param name="options">Platform configuration options, including <c>AdminSubjectIds</c>.</param>
    public PlatformAdminBootstrapService(NocturneDbContext db, IOptions<PlatformOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    /// <summary>
    /// Grants platform admin status according to the configured priority rules.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        // Option 1: explicit config takes precedence
        if (_options.AdminSubjectIds.Count > 0)
        {
            await _db.Subjects
                .Where(s => _options.AdminSubjectIds.Contains(s.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPlatformAdmin, true), cancellationToken);
            return;
        }

        // No-op if a platform admin already exists
        if (await _db.Subjects.AnyAsync(s => s.IsPlatformAdmin, cancellationToken))
            return;

        // Option 2: grant to the owner of the oldest tenant that has one. Resolved by walking
        // tenants oldest-first and looking the owner up under each tenant's own pin, because a
        // single query over every tenant's memberships has no tenant to be pinned to.
        var firstOwnerSubjectId = await FindOldestTenantOwnerAsync(cancellationToken);
        if (firstOwnerSubjectId is null) return;

        await _db.Subjects
            .Where(s => s.Id == firstOwnerSubjectId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPlatformAdmin, true), cancellationToken);
    }

    /// <summary>
    /// The subject holding the owner role on the oldest tenant that has one, or
    /// <see langword="null"/> when no tenant does. Tenants without an owner are skipped, matching
    /// the ordered membership scan this replaces.
    /// </summary>
    private async Task<Guid?> FindOldestTenantOwnerAsync(CancellationToken cancellationToken)
    {
        // tenants is not tenant-scoped, so the ordering can be resolved unpinned.
        var tenantIds = await _db.Tenants
            .OrderBy(t => t.SysCreatedAt)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            await _db.PinTenantAsync(tenantId, cancellationToken);

            var ownerSubjectId = await _db.TenantMembers
                .Where(tm => tm.TenantId == tenantId
                    && tm.MemberRoles.Any(mr => mr.TenantRole!.Slug == TenantPermissions.SeedRoles.Owner))
                .Select(tm => tm.SubjectId)
                .FirstOrDefaultAsync(cancellationToken);

            if (ownerSubjectId != default) return ownerSubjectId;
        }

        return null;
    }
}
