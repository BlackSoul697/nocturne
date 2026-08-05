using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nocturne.Infrastructure.Data.Entities;
using Xunit;

namespace Nocturne.Infrastructure.Data.Tests;

/// <summary>
/// Membership and role writes now route through the tenant-ownership enforcement in
/// <c>SaveChanges</c>, because <see cref="TenantMemberEntity"/> and <see cref="TenantRoleEntity"/>
/// are <see cref="ITenantScoped"/>. This pins what that enforcement does to them: a modification
/// from the wrong tenant context is refused, an insert with no tenant to stamp is refused, and the
/// ordinary same-tenant write is untouched.
/// </summary>
/// <remarks>
/// This is the in-process half of the guard and it fails loudly; the PostgreSQL WITH CHECK clause is
/// the other half, covered by the RLS integration tests, and is what catches a write that never
/// passes through the change tracker.
/// </remarks>
public class TenantMembershipOwnershipTests
{
    [Fact]
    public async Task ModifyingAMembershipFromAnotherTenantsContext_IsRefused()
    {
        var options = NewStore();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var memberId = Guid.CreateVersion7();

        await using (var seed = new NocturneDbContext(options) { TenantId = tenantB })
        {
            seed.TenantMembers.Add(new TenantMemberEntity
            {
                Id = memberId,
                TenantId = tenantB,
                SubjectId = Guid.NewGuid(),
            });
            await seed.SaveChangesAsync();
        }

        // Reached without the tenant filter, as a cross-tenant read would reach it.
        await using var ctx = new NocturneDbContext(options) { TenantId = tenantA };
        var member = await ctx.TenantMembers
            .IgnoreQueryFilters()
            .SingleAsync(m => m.Id == memberId);
        member.LimitTo24Hours = true;

        var act = () => ctx.SaveChangesAsync();

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*belonging to tenant*", "a membership may only be modified from its own tenant");
    }

    [Fact]
    public async Task ModifyingAMembershipFromItsOwnTenantsContext_IsAllowed()
    {
        var options = NewStore();
        var tenantId = Guid.NewGuid();
        var memberId = Guid.CreateVersion7();

        await using (var seed = new NocturneDbContext(options) { TenantId = tenantId })
        {
            seed.TenantMembers.Add(new TenantMemberEntity
            {
                Id = memberId,
                TenantId = tenantId,
                SubjectId = Guid.NewGuid(),
            });
            await seed.SaveChangesAsync();
        }

        await using var ctx = new NocturneDbContext(options) { TenantId = tenantId };
        var member = await ctx.TenantMembers.SingleAsync(m => m.Id == memberId);
        member.LimitTo24Hours = true;
        await ctx.SaveChangesAsync();

        await using var verify = new NocturneDbContext(options) { TenantId = tenantId };
        (await verify.TenantMembers.SingleAsync(m => m.Id == memberId)).LimitTo24Hours.Should().BeTrue();
    }

    /// <summary>
    /// Every membership and role write in the codebase sets the tenant explicitly, so this path is
    /// only reachable by a new caller forgetting to — which is exactly when it should fail rather
    /// than write a row no tenant can reach.
    /// </summary>
    [Fact]
    public async Task InsertingARoleWithNoTenantAndNoContextPin_IsRefused()
    {
        await using var ctx = new NocturneDbContext(NewStore());
        ctx.TenantRoles.Add(new TenantRoleEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "Orphan",
            Slug = "orphan",
            Permissions = [],
        });

        var act = () => ctx.SaveChangesAsync();

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*without a TenantId*");
    }

    [Fact]
    public async Task InsertingAMembershipWithNoTenant_StampsThePinnedOne()
    {
        var options = NewStore();
        var tenantId = Guid.NewGuid();
        var memberId = Guid.CreateVersion7();

        await using var ctx = new NocturneDbContext(options) { TenantId = tenantId };
        ctx.TenantMembers.Add(new TenantMemberEntity { Id = memberId, SubjectId = Guid.NewGuid() });
        await ctx.SaveChangesAsync();

        await using var verify = new NocturneDbContext(options) { TenantId = tenantId };
        (await verify.TenantMembers.SingleAsync(m => m.Id == memberId)).TenantId.Should().Be(tenantId);
    }

    private static DbContextOptions<NocturneDbContext> NewStore() =>
        new DbContextOptionsBuilder<NocturneDbContext>()
            .UseInMemoryDatabase($"membership_ownership_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
}
