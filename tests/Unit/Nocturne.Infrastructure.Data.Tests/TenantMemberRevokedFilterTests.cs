using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nocturne.Infrastructure.Data.Entities;
using Xunit;

namespace Nocturne.Infrastructure.Data.Tests;

/// <summary>
/// Verifies how the named query filters on <see cref="TenantMemberEntity"/> compose: tenant
/// isolation (<see cref="NocturneDbContext.TenantFilterKey"/>) and active membership
/// (<see cref="NocturneDbContext.RevokedMembershipFilterKey"/>). Every membership check across the
/// auth gates relies on these rather than repeating the predicates.
/// </summary>
/// <remarks>
/// These filters are the EF half of the enforcement; the PostgreSQL policies are the other half and
/// are covered by the RLS integration tests. The in-memory provider has no Row Level Security, so
/// nothing here proves a policy — what it does prove is which rows EF asks for, which is what
/// decides whether a cross-tenant read reaches its rows at all.
/// </remarks>
public class TenantMemberRevokedFilterTests
{
    [Fact]
    public async Task TenantMembers_AreExcludedWhenRevoked()
    {
        var options = NewStore();
        var tenantId = Guid.NewGuid();
        var activeSubject = Guid.NewGuid();
        var revokedSubject = Guid.NewGuid();

        await using (var seedCtx = new NocturneDbContext(options))
        {
            seedCtx.TenantMembers.AddRange(
                NewMember(tenantId, activeSubject, revokedAt: null),
                NewMember(tenantId, revokedSubject, revokedAt: DateTime.UtcNow));
            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = new NocturneDbContext(options) { TenantId = tenantId };

        var visible = await ctx.TenantMembers.Select(m => m.SubjectId).ToListAsync();
        visible.Should().BeEquivalentTo(new[] { activeSubject },
            "the global query filter must hide revoked memberships from every membership query");

        var all = await ctx.TenantMembers.IgnoreQueryFilters().Select(m => m.SubjectId).ToListAsync();
        all.Should().BeEquivalentTo(new[] { activeSubject, revokedSubject },
            "IgnoreQueryFilters bypasses the filter, confirming the revoked row exists but is filtered");
    }

    [Fact]
    public async Task TenantMembers_OfAnotherTenant_AreExcluded()
    {
        var options = NewStore();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var subject = Guid.NewGuid();

        await using (var seedCtx = new NocturneDbContext(options))
        {
            seedCtx.TenantMembers.AddRange(
                NewMember(tenantA, subject, revokedAt: null),
                NewMember(tenantB, subject, revokedAt: null));
            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = new NocturneDbContext(options) { TenantId = tenantA };

        var visible = await ctx.TenantMembers.Select(m => m.TenantId).ToListAsync();
        visible.Should().BeEquivalentTo(new[] { tenantA },
            "tenant isolation must hide another tenant's membership for the same subject");
    }

    /// <summary>
    /// The shape the subject-scoped cross-tenant reads use. Dropping tenant isolation by key must
    /// reach the subject's memberships in every tenant while still hiding revoked ones — a revoked
    /// membership reappearing in the tenant switcher or the enrolment probe would be an
    /// authorization regression, which the no-argument overload would cause.
    /// </summary>
    [Fact]
    public async Task SkippingTenantIsolationByKey_KeepsTheRevokedFilter()
    {
        var options = NewStore();
        var pinnedTenant = Guid.NewGuid();
        var otherActiveTenant = Guid.NewGuid();
        var otherRevokedTenant = Guid.NewGuid();
        var subject = Guid.NewGuid();

        await using (var seedCtx = new NocturneDbContext(options))
        {
            seedCtx.TenantMembers.AddRange(
                NewMember(otherActiveTenant, subject, revokedAt: null),
                NewMember(otherRevokedTenant, subject, revokedAt: DateTime.UtcNow));
            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = new NocturneDbContext(options) { TenantId = pinnedTenant, SubjectId = subject };

        var reached = await ctx.TenantMembers
            .IgnoreQueryFilters([NocturneDbContext.TenantFilterKey])
            .Where(m => m.SubjectId == subject)
            .Select(m => m.TenantId)
            .ToListAsync();

        reached.Should().BeEquivalentTo(new[] { otherActiveTenant },
            "skipping tenant isolation by key must cross tenants but keep revoked memberships hidden");
    }

    /// <summary>
    /// The caregiver-overview shape. <see cref="TenantRoleEntity"/> is tenant-scoped too, so if
    /// skipping tenant isolation by key did not reach the included navigation the role would come
    /// back null and the caller's <c>Permissions</c> read would throw.
    /// </summary>
    [Fact]
    public async Task SkippingTenantIsolationByKey_ReachesTheIncludedTenantRole()
    {
        var options = NewStore();
        var pinnedTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var memberId = Guid.CreateVersion7();
        var roleId = Guid.CreateVersion7();

        await using (var seedCtx = new NocturneDbContext(options))
        {
            seedCtx.TenantRoles.Add(new TenantRoleEntity
            {
                Id = roleId,
                TenantId = otherTenant,
                Name = "Viewer",
                Slug = "viewer",
                Permissions = ["glucose.read"],
            });
            seedCtx.TenantMembers.Add(new TenantMemberEntity
            {
                Id = memberId,
                TenantId = otherTenant,
                SubjectId = subject,
            });
            seedCtx.TenantMemberRoles.Add(new TenantMemberRoleEntity
            {
                Id = Guid.CreateVersion7(),
                TenantMemberId = memberId,
                TenantRoleId = roleId,
            });
            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = new NocturneDbContext(options) { TenantId = pinnedTenant, SubjectId = subject };

        var memberships = await ctx.TenantMembers.AsNoTracking()
            .IgnoreQueryFilters([NocturneDbContext.TenantFilterKey])
            .Include(tm => tm.MemberRoles).ThenInclude(mr => mr.TenantRole)
            .Where(tm => tm.SubjectId == subject)
            .ToListAsync();

        var membership = memberships.Should().ContainSingle().Subject;
        var memberRole = membership.MemberRoles.Should().ContainSingle().Subject;
        memberRole.TenantRole.Should().NotBeNull(
            "the filter key is dropped across the whole query, including the tenant-scoped role");
        memberRole.TenantRole.Permissions.Should().BeEquivalentTo(["glucose.read"]);
    }

    private static DbContextOptions<NocturneDbContext> NewStore() =>
        new DbContextOptionsBuilder<NocturneDbContext>()
            .UseInMemoryDatabase($"tenant_member_revoked_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static TenantMemberEntity NewMember(Guid tenantId, Guid subjectId, DateTime? revokedAt) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = tenantId,
        SubjectId = subjectId,
        RevokedAt = revokedAt,
    };
}
