using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nocturne.API.Services.Identity;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Tests.Shared.Infrastructure;

namespace Nocturne.API.Tests.Services.Identity;

/// <summary>
/// In-memory provider tests: they exercise the service's own tenant predicates and the context
/// pin it sets, not the PostgreSQL Row Level Security policies, which have no equivalent here.
/// </summary>
public class TenantRoleServiceTests : IDisposable
{
    private readonly NocturneDbContext _context;
    private readonly TenantRoleService _service;
    private readonly Guid _tenantId = Guid.CreateVersion7();

    /// <summary>
    /// Hands out contexts over the same in-memory database, so the seed context
    /// <see cref="TenantRoleService.SeedRolesForTenantAsync"/> creates writes where the test's
    /// own context can read it.
    /// </summary>
    private sealed class SharedInMemoryFactory(string dbName) : IDbContextFactory<NocturneDbContext>
    {
        public List<NocturneDbContext> Handed { get; } = [];

        public NocturneDbContext CreateDbContext()
        {
            var context = TestDbContextFactory.CreateInMemoryContext(dbName);
            Handed.Add(context);
            return context;
        }
    }

    private readonly SharedInMemoryFactory _factory;
    private readonly string _dbName;

    public TenantRoleServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var factory = new SharedInMemoryFactory(_dbName);
        _factory = factory;
        // The CRUD methods run on the injected request-scoped context, which every route reaching
        // them has resolved a tenant for; tenant_roles carries a query filter on that pin.
        _context = TestDbContextFactory.CreateInMemoryContext(_dbName, _tenantId);
        _context.Tenants.Add(new TenantEntity
        {
            Id = _tenantId,
            Slug = "test",
            DisplayName = "Test Tenant",
        });
        _context.SaveChanges();
        _service = new TenantRoleService(_context, factory);
    }

    [Fact]
    public async Task SeedRolesForTenantAsync_CreatesAllSixSeedRoles()
    {
        await _service.SeedRolesForTenantAsync(_tenantId);
        var roles = await _context.TenantRoles.Where(r => r.TenantId == _tenantId).ToListAsync();
        roles.Should().HaveCount(6);
        roles.Should().Contain(r => r.Slug == "owner" && r.IsSystem);
        roles.Should().Contain(r => r.Slug == "admin" && r.IsSystem);
        roles.Should().Contain(r => r.Slug == "caretaker" && r.IsSystem);
        roles.Should().Contain(r => r.Slug == "viewer" && r.IsSystem);
        roles.Should().Contain(r => r.Slug == "clinician" && r.IsSystem);
        roles.Should().Contain(r => r.Slug == "denied" && r.IsSystem);
    }

    [Fact]
    public async Task SeedRolesForTenantAsync_SeedsOnAContextPinnedToTheTenant()
    {
        // Every caller of the seed is a tenant-creation flow reached without a resolved tenant, so
        // the injected context carries no pin. Reproduce that rather than using the pinned fixture
        // one: writing the seed through an unpinned context is exactly the failure being excluded.
        var unpinnedFactory = new SharedInMemoryFactory(_dbName);
        await using var unpinnedContext = TestDbContextFactory.CreateInMemoryContext(_dbName);
        var service = new TenantRoleService(unpinnedContext, unpinnedFactory);

        unpinnedContext.TenantId.Should().Be(Guid.Empty);

        await service.SeedRolesForTenantAsync(_tenantId);

        unpinnedFactory.Handed.Should().ContainSingle("the seed takes exactly one context of its own");
        unpinnedFactory.Handed[0].TenantId.Should().Be(_tenantId);
        unpinnedContext.TenantId.Should().Be(
            Guid.Empty, "the request-scoped context must not be re-pinned");

        // And the rows landed where the pinned fixture context can see them.
        (await _context.TenantRoles.CountAsync(r => r.TenantId == _tenantId)).Should().Be(6);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_ForAnUnreachableMember_ReturnsNoPermissions()
    {
        // A membership id the context cannot resolve is a refusal, not a fault.
        var effective = await _service.GetEffectivePermissionsAsync(Guid.CreateVersion7());

        effective.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateRoleAsync_CreatesCustomRole()
    {
        var result = await _service.CreateRoleAsync(_tenantId, "School Nurse", "Read-only for school staff", ["glucose.read", "reports.read"]);
        result.Name.Should().Be("School Nurse");
        result.Slug.Should().Be("school-nurse");
        result.Description.Should().Be("Read-only for school staff");
        result.Permissions.Should().BeEquivalentTo(["glucose.read", "reports.read"]);
        result.IsSystem.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteRoleAsync_BlocksOwnerDeletion()
    {
        await _service.SeedRolesForTenantAsync(_tenantId);
        var ownerRole = await _context.TenantRoles.FirstAsync(r => r.Slug == "owner" && r.TenantId == _tenantId);
        var result = await _service.DeleteRoleAsync(_tenantId, ownerRole.Id);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("owner_role_protected");
    }

    [Fact]
    public async Task DeleteRoleAsync_RemovesRoleFromMembers()
    {
        await _service.SeedRolesForTenantAsync(_tenantId);
        var followerRole = await _context.TenantRoles.FirstAsync(r => r.Slug == "viewer" && r.TenantId == _tenantId);
        var caretakerRole = await _context.TenantRoles.FirstAsync(r => r.Slug == "caretaker" && r.TenantId == _tenantId);

        var member = new TenantMemberEntity { Id = Guid.CreateVersion7(), TenantId = _tenantId, SubjectId = Guid.CreateVersion7() };
        _context.TenantMembers.Add(member);
        _context.TenantMemberRoles.AddRange(
            new TenantMemberRoleEntity { Id = Guid.CreateVersion7(), TenantMemberId = member.Id, TenantRoleId = followerRole.Id },
            new TenantMemberRoleEntity { Id = Guid.CreateVersion7(), TenantMemberId = member.Id, TenantRoleId = caretakerRole.Id }
        );
        await _context.SaveChangesAsync();

        var result = await _service.DeleteRoleAsync(_tenantId, followerRole.Id);
        result.Success.Should().BeTrue();

        var remainingRoles = await _context.TenantMemberRoles.Where(mr => mr.TenantMemberId == member.Id).ToListAsync();
        remainingRoles.Should().HaveCount(1);
        remainingRoles[0].TenantRoleId.Should().Be(caretakerRole.Id);
    }

    [Fact]
    public async Task DeleteRoleAsync_BlocksIfMemberWouldHaveZeroPermissions()
    {
        await _service.SeedRolesForTenantAsync(_tenantId);
        var followerRole = await _context.TenantRoles.FirstAsync(r => r.Slug == "viewer" && r.TenantId == _tenantId);

        var member = new TenantMemberEntity { Id = Guid.CreateVersion7(), TenantId = _tenantId, SubjectId = Guid.CreateVersion7() };
        _context.TenantMembers.Add(member);
        _context.TenantMemberRoles.Add(new TenantMemberRoleEntity { Id = Guid.CreateVersion7(), TenantMemberId = member.Id, TenantRoleId = followerRole.Id });
        await _context.SaveChangesAsync();

        var result = await _service.DeleteRoleAsync(_tenantId, followerRole.Id);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("members_would_lose_all_permissions");
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_UnionsRolesAndDirectPermissions()
    {
        await _service.SeedRolesForTenantAsync(_tenantId);
        var followerRole = await _context.TenantRoles.FirstAsync(r => r.Slug == "viewer" && r.TenantId == _tenantId);

        var member = new TenantMemberEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenantId,
            SubjectId = Guid.CreateVersion7(),
            DirectPermissions = ["treatments.read"],
        };
        _context.TenantMembers.Add(member);
        _context.TenantMemberRoles.Add(new TenantMemberRoleEntity { Id = Guid.CreateVersion7(), TenantMemberId = member.Id, TenantRoleId = followerRole.Id });
        await _context.SaveChangesAsync();

        var effective = await _service.GetEffectivePermissionsAsync(member.Id);
        effective.Should().BeEquivalentTo(
            ["glucose.read", "reports.read", "device.notify", "device.actuate", "treatments.read"]);
    }

    [Fact]
    public async Task UpdateRoleAsync_ReturnsNull_ForAnotherTenantsRole()
    {
        var otherRoleId = await SeedOtherTenantViewerRoleAsync();

        var result = await _service.UpdateRoleAsync(
            _tenantId, otherRoleId, "Pwned", null, [TenantPermissions.Superuser]);

        result.Should().BeNull("a role ID from another tenant must not resolve");

        await using var otherTenantView = TestDbContextFactory.CreateInMemoryContext(_dbName, _otherTenantId);
        var untouched = await otherTenantView.TenantRoles.AsNoTracking().FirstAsync(r => r.Id == otherRoleId);
        untouched.Name.Should().Be("Viewer");
        untouched.Permissions.Should().BeEquivalentTo([TenantPermissions.GlucoseRead]);
    }

    [Fact]
    public async Task DeleteRoleAsync_ReportsNotFound_ForAnotherTenantsRole()
    {
        var otherRoleId = await SeedOtherTenantViewerRoleAsync();

        var result = await _service.DeleteRoleAsync(_tenantId, otherRoleId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("role_not_found");
        await using var otherTenantView = TestDbContextFactory.CreateInMemoryContext(_dbName, _otherTenantId);
        (await otherTenantView.TenantRoles.AsNoTracking().AnyAsync(r => r.Id == otherRoleId))
            .Should().BeTrue();
    }

    [Fact]
    public async Task GetRoleByIdAsync_ReturnsNull_ForAnotherTenantsRole()
    {
        var otherRoleId = await SeedOtherTenantViewerRoleAsync();

        (await _service.GetRoleByIdAsync(_tenantId, otherRoleId)).Should().BeNull();
    }

    private readonly Guid _otherTenantId = Guid.CreateVersion7();

    private async Task<Guid> SeedOtherTenantViewerRoleAsync()
    {
        var otherTenantId = _otherTenantId;
        _context.Tenants.Add(new TenantEntity
        {
            Id = otherTenantId,
            Slug = "other",
            DisplayName = "Other Tenant",
        });

        var role = new TenantRoleEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = otherTenantId,
            Name = "Viewer",
            Slug = "viewer",
            Permissions = [TenantPermissions.GlucoseRead],
            IsSystem = true,
        };
        _context.TenantRoles.Add(role);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        return role.Id;
    }

    public void Dispose() => _context.Dispose();
}
