using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.API.Controllers.V4;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V4;

/// <summary>
/// Tests for the setup flow, focusing on the soft-lock scenario where a tenant
/// exists but owner passkey registration was never completed.
/// </summary>
public class SetupControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<NocturneDbContext> _dbOptions;
    private readonly NocturneDbContext _dbContext;
    private readonly Mock<ITenantService> _tenantService;
    private readonly Mock<IPasskeyService> _passkeyService;
    private readonly Mock<IRecoveryCodeService> _recoveryCodeService;
    private readonly Mock<IJwtService> _jwtService;
    private readonly Mock<IRefreshTokenService> _refreshTokenService;
    private readonly Mock<ISubjectService> _subjectService;
    private readonly Mock<IOidcAuthService> _oidcAuthService;
    private readonly SetupController _controller;

    public SetupControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _dbContext = new NocturneDbContext(_dbOptions);
        _dbContext.Database.EnsureCreated();

        _tenantService = new Mock<ITenantService>();
        _passkeyService = new Mock<IPasskeyService>();
        _recoveryCodeService = new Mock<IRecoveryCodeService>();
        _jwtService = new Mock<IJwtService>();
        _refreshTokenService = new Mock<IRefreshTokenService>();
        _subjectService = new Mock<ISubjectService>();
        _oidcAuthService = new Mock<IOidcAuthService>();

        var oidcOptions = Options.Create(new OidcOptions
        {
            Cookie = new CookieSettings
            {
                AccessTokenName = ".Nocturne.AccessToken",
                RefreshTokenName = ".Nocturne.RefreshToken",
                Secure = true,
            },
        });

        var dbFactory = new Mock<IDbContextFactory<NocturneDbContext>>();
        dbFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var ctx = new NocturneDbContext(_dbOptions);
                return ctx;
            });

        _controller = new SetupController(
            _tenantService.Object,
            _passkeyService.Object,
            _recoveryCodeService.Object,
            _jwtService.Object,
            _refreshTokenService.Object,
            _subjectService.Object,
            dbFactory.Object,
            oidcOptions,
            _oidcAuthService.Object,
            Options.Create(new Nocturne.API.Multitenancy.MultitenancyConfiguration()),
            new Mock<IHttpClientFactory>().Object,
            new Mock<ILogger<SetupController>>().Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    // ── CreateTenant ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTenant_WhenNoTenantsExist_Succeeds()
    {
        // Arrange
        var tenantId = Guid.CreateVersion7();
        _tenantService.Setup(s => s.ValidateSlugAsync("fresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugValidationResult(true));
        _tenantService.Setup(s => s.CreateWithoutOwnerAsync("fresh", "Fresh Instance", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantCreatedDto(tenantId, "fresh", "Fresh Instance", true, DateTime.UtcNow));

        // Act
        var result = await _controller.CreateTenant(
            new SetupTenantRequest("fresh", "Fresh Instance"), CancellationToken.None);

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SetupTenantResponse>().Subject;
        response.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task CreateTenant_WhenTenantAlreadyExists_Returns409()
    {
        // Arrange — seed a tenant so count > 0
        _dbContext.Set<TenantEntity>().Add(new TenantEntity
        {
            Id = Guid.CreateVersion7(),
            Slug = "existing",
            DisplayName = "Existing Tenant",
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.CreateTenant(
            new SetupTenantRequest("new-slug", "New Instance"), CancellationToken.None);

        // Assert — this is the 409 that causes the soft-lock
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CreateTenant_WhenTenantAlreadyExists_WithSameSlug_Returns409()
    {
        // Arrange — the user tries to re-submit with the same slug
        _dbContext.Set<TenantEntity>().Add(new TenantEntity
        {
            Id = Guid.CreateVersion7(),
            Slug = "my-instance",
            DisplayName = "My Instance",
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.CreateTenant(
            new SetupTenantRequest("my-instance", "My Instance"), CancellationToken.None);

        // Assert — still 409, not "slug taken" — the guard is tenant count, not slug uniqueness
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CreateTenant_WhenMultipleTenantsExist_Returns409()
    {
        // Arrange — edge case: somehow multiple tenants exist
        _dbContext.Set<TenantEntity>().Add(new TenantEntity
        {
            Id = Guid.CreateVersion7(),
            Slug = "tenant-a",
            DisplayName = "Tenant A",
        });
        _dbContext.Set<TenantEntity>().Add(new TenantEntity
        {
            Id = Guid.CreateVersion7(),
            Slug = "tenant-b",
            DisplayName = "Tenant B",
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.CreateTenant(
            new SetupTenantRequest("tenant-c", "Tenant C"), CancellationToken.None);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CreateTenant_WithInvalidSlug_Returns400()
    {
        // Arrange — no tenants, but slug validation fails
        _tenantService.Setup(s => s.ValidateSlugAsync("bad!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SlugValidationResult(false, "Invalid characters"));

        // Act
        var result = await _controller.CreateTenant(
            new SetupTenantRequest("bad!", "Bad Slug"), CancellationToken.None);

        // Assert
        var problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateTenant_WithEmptySlug_Returns400()
    {
        // Act
        var result = await _controller.CreateTenant(
            new SetupTenantRequest("", "Empty Slug"), CancellationToken.None);

        // Assert
        var problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateTenant_WithEmptyDisplayName_Returns400()
    {
        // Act
        var result = await _controller.CreateTenant(
            new SetupTenantRequest("valid-slug", ""), CancellationToken.None);

        // Assert
        var problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(400);
    }

    // ── OwnerOptions (soft-lock preconditions) ────────────────────────────

    [Fact]
    public async Task OwnerOptions_WhenNoTenantsExist_Returns409()
    {
        // Arrange — no tenants at all (user skipped tenant creation somehow)
        var request = new SetupOwnerOptionsRequest
        {
            Username = "admin",
            DisplayName = "Admin User",
        };

        // Act
        var result = await _controller.OwnerOptions(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task OwnerOptions_WhenMultipleTenantsExist_Returns409()
    {
        // Arrange
        _dbContext.Set<TenantEntity>().Add(new TenantEntity
        {
            Id = Guid.CreateVersion7(), Slug = "a", DisplayName = "A",
        });
        _dbContext.Set<TenantEntity>().Add(new TenantEntity
        {
            Id = Guid.CreateVersion7(), Slug = "b", DisplayName = "B",
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _controller.OwnerOptions(
            new SetupOwnerOptionsRequest { Username = "admin", DisplayName = "Admin" },
            CancellationToken.None);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    // OwnerOptions tests that require a sole tenant with members are skipped
    // in unit tests because GetSoleTenantWithoutOwnerAsync calls set_config(),
    // a PostgreSQL-only function. These scenarios are covered by integration tests.

    // ── Soft-lock scenario: the full sequence ─────────────────────────────

    [Fact]
    public async Task SoftLock_TenantCreatedButOwnerNeverCompleted_CreateTenantRejects()
    {
        // This is the exact soft-lock scenario:
        // 1. User visits /setup, creates a tenant (succeeds)
        // 2. User's browser crashes / they close the tab
        // 3. Tenant exists but has no passkey credentials
        // 4. TenantSetupMiddleware returns 503 → frontend redirects to /setup
        // 5. Setup page shows tenant creation form again
        // 6. User enters a new slug → CreateTenant returns 409

        // Step 1: Create the tenant (simulating what happened before the crash)
        _dbContext.Set<TenantEntity>().Add(new TenantEntity
        {
            Id = Guid.CreateVersion7(),
            Slug = "my-instance",
            DisplayName = "My Instance",
        });
        await _dbContext.SaveChangesAsync();

        // Step 6: User tries to create a tenant again after being redirected to /setup
        var result = await _controller.CreateTenant(
            new SetupTenantRequest("different-slug", "Different Instance"),
            CancellationToken.None);

        // Assert — this is the soft-lock: 409 Conflict because a tenant exists
        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var body = conflict.Value;
        body.Should().NotBeNull();
        body!.ToString().Should().Contain("setup_already_complete");
    }

    [Fact]
    public async Task SoftLock_TenantCreatedAndOwnerSubjectCreated_ButPasskeyFailed_CreateTenantRejects()
    {
        // More advanced soft-lock: tenant AND subject were created (OwnerOptions
        // ran successfully) but the WebAuthn ceremony failed or was abandoned.
        // The subject exists as a tenant member but has no passkey.
        // CreateTenant is blocked by the tenant count > 0 guard.
        // OwnerOptions is also blocked (tested separately in integration tests
        // because it requires set_config, a PostgreSQL-only function).

        var tenantId = Guid.CreateVersion7();
        var subjectId = Guid.CreateVersion7();

        _dbContext.Set<TenantEntity>().Add(new TenantEntity
        {
            Id = tenantId, Slug = "my-instance", DisplayName = "My Instance",
        });
        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = subjectId, Name = "Incomplete Owner",
            IsActive = true, IsSystemSubject = false,
        });
        _dbContext.TenantMembers.Add(new TenantMemberEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            SubjectId = subjectId,
        });
        await _dbContext.SaveChangesAsync();

        // CreateTenant is blocked — this is the soft-lock
        var createResult = await _controller.CreateTenant(
            new SetupTenantRequest("any-slug", "Any Name"), CancellationToken.None);
        createResult.Should().BeOfType<ConflictObjectResult>();
    }

    // SoftLock_TenantWithOnlySystemMembers_OwnerOptionsSucceeds is an
    // integration test — it requires PostgreSQL's set_config() function
    // which is not available in SQLite.

    // ── ValidateUsername ──────────────────────────────────────────────────
    // Tests that hit the DB after format checks (valid usernames) are skipped
    // because ValidateUsername calls ExecuteSqlRawAsync("set_config(...)"),
    // a PostgreSQL-only function not available in SQLite.

    [Theory]
    [InlineData("ab")]           // too short
    [InlineData("-bad")]         // leading hyphen
    [InlineData("bad-")]         // trailing hyphen
    [InlineData(".bad")]         // leading dot
    [InlineData("bad.")]         // trailing dot
    [InlineData("has spaces")]   // spaces
    public async Task ValidateUsername_WhenInvalidFormat_ReturnsError(string username)
    {
        _dbContext.Set<TenantEntity>().Add(new TenantEntity
        {
            Id = Guid.CreateVersion7(), Slug = "test", DisplayName = "Test",
        });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.ValidateUsername(username, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var validation = ok.Value.Should().BeOfType<SlugValidationResult>().Subject;
        validation.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("system")]
    public async Task ValidateUsername_WhenReserved_ReturnsError(string username)
    {
        _dbContext.Set<TenantEntity>().Add(new TenantEntity
        {
            Id = Guid.CreateVersion7(), Slug = "test", DisplayName = "Test",
        });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.ValidateUsername(username, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var validation = ok.Value.Should().BeOfType<SlugValidationResult>().Subject;
        validation.IsValid.Should().BeFalse();
        validation.Message.Should().Contain("reserved");
    }

    [Fact]
    public async Task ValidateUsername_WhenEmpty_ReturnsError()
    {
        var result = await _controller.ValidateUsername("", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var validation = ok.Value.Should().BeOfType<SlugValidationResult>().Subject;
        validation.IsValid.Should().BeFalse();
    }

    // ── OwnerOidc ────────────────────────────────────────────────────────

    [Fact]
    public async Task OwnerOidc_WhenNoTenantsExist_Returns409()
    {
        var request = new SetupOwnerOidcRequest
        {
            Username = "admin",
            DisplayName = "Admin User",
            ProviderId = Guid.CreateVersion7(),
        };

        var result = await _controller.OwnerOidc(request, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task OwnerOidc_WhenMultipleTenantsExist_Returns409()
    {
        _dbContext.Set<TenantEntity>().Add(new TenantEntity
        {
            Id = Guid.CreateVersion7(), Slug = "a", DisplayName = "A",
        });
        _dbContext.Set<TenantEntity>().Add(new TenantEntity
        {
            Id = Guid.CreateVersion7(), Slug = "b", DisplayName = "B",
        });
        await _dbContext.SaveChangesAsync();

        var result = await _controller.OwnerOidc(
            new SetupOwnerOidcRequest { Username = "admin", DisplayName = "Admin", ProviderId = Guid.CreateVersion7() },
            CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // OwnerOidc tests that require a sole tenant (e.g. validation of empty
    // username/ProviderId) are skipped in unit tests because
    // GetSoleTenantWithoutOwnerAsync calls set_config(), a PostgreSQL-only
    // function. These scenarios are covered by integration tests.
}
