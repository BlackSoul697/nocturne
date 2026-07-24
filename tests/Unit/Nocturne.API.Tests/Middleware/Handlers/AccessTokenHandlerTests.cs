using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Middleware.Handlers;
using Nocturne.API.Services.Auth;
using Nocturne.Core.Contracts.Auth;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Xunit;

namespace Nocturne.API.Tests.Middleware.Handlers;

/// <summary>
/// Tests for <see cref="AccessTokenHandler"/>, covering both token shapes it accepts:
/// Nocturne-minted (64 hex, no dash) and imported Nightscout ({name}-{digest}).
/// </summary>
public class AccessTokenHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<NocturneDbContext> _dbOptions;
    private readonly NocturneDbContext _dbContext;
    private readonly SubjectService _subjectService;
    private readonly AccessTokenHandler _handler;

    private readonly Guid _testTenantId = Guid.CreateVersion7();

    public AccessTokenHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _dbContext = new NocturneDbContext(_dbOptions) { TenantId = _testTenantId };
        _dbContext.Database.EnsureCreated();

        _subjectService = new SubjectService(
            _dbContext,
            Mock.Of<IAuthAuditService>(),
            Mock.Of<ILogger<SubjectService>>());

        // The handler resolves ISubjectService out of a fresh scope; hand it the real
        // service so mint and validate exercise the same hashing path.
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(p => p.GetService(typeof(ISubjectService)))
            .Returns(_subjectService);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        _handler = new AccessTokenHandler(
            scopeFactory.Object,
            Mock.Of<ILogger<AccessTokenHandler>>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Regression test for #544: a token minted by <see cref="SubjectService"/> has no dash,
    /// so the old format pre-filter skipped it and the token could never authenticate a
    /// normal API request. Mints through the real service rather than hard-coding a shape,
    /// so it fails if generation and validation drift apart again.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_MintedTokenViaQueryParam_ReturnsSuccess()
    {
        var (subjectId, token) = await MintDeviceSubjectAsync("uploader");

        var context = CreateHttpContext();
        context.Request.QueryString = new QueryString($"?token={token}");

        var result = await _handler.AuthenticateAsync(context);

        Assert.True(result.Succeeded);
        Assert.Equal(AuthType.LegacyAccessToken, result.AuthContext!.AuthType);
        Assert.Equal(subjectId, result.AuthContext.SubjectId);
    }

    [Fact]
    public async Task AuthenticateAsync_MintedTokenViaBearerHeader_ReturnsSuccess()
    {
        var (subjectId, token) = await MintDeviceSubjectAsync("bearer-uploader");

        var context = CreateHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";

        var result = await _handler.AuthenticateAsync(context);

        Assert.True(result.Succeeded);
        Assert.Equal(subjectId, result.AuthContext!.SubjectId);
    }

    [Fact]
    public async Task AuthenticateAsync_RegeneratedToken_ReturnsSuccess()
    {
        var (subjectId, originalToken) = await MintDeviceSubjectAsync("rotated");
        var newToken = await _subjectService.RegenerateAccessTokenAsync(subjectId);
        Assert.NotNull(newToken);

        var context = CreateHttpContext();
        context.Request.QueryString = new QueryString($"?token={newToken}");
        var result = await _handler.AuthenticateAsync(context);

        Assert.True(result.Succeeded);
        Assert.Equal(subjectId, result.AuthContext!.SubjectId);

        // The superseded token must stop working.
        var staleContext = CreateHttpContext();
        staleContext.Request.QueryString = new QueryString($"?token={originalToken}");
        var staleResult = await _handler.AuthenticateAsync(staleContext);

        Assert.False(staleResult.Succeeded);
    }

    /// <summary>
    /// Tokens imported from classic Nightscout keep their original {name}-{digest} shape
    /// (MigrationJobService stores a hash of the original value), so that branch must survive.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_ImportedNightscoutShapedToken_ReturnsSuccess()
    {
        const string token = "rhys-a1b2c3d4e5f6a7b8";
        var subjectId = await SeedSubjectWithTokenAsync("rhys", token);

        var context = CreateHttpContext();
        context.Request.QueryString = new QueryString($"?token={token}");

        var result = await _handler.AuthenticateAsync(context);

        Assert.True(result.Succeeded);
        Assert.Equal(subjectId, result.AuthContext!.SubjectId);
    }

    [Fact]
    public async Task AuthenticateAsync_DeactivatedSubject_DoesNotAuthenticate()
    {
        var (subjectId, token) = await MintDeviceSubjectAsync("disabled");
        await _subjectService.DeactivateSubjectAsync(subjectId);

        var context = CreateHttpContext();
        context.Request.QueryString = new QueryString($"?token={token}");

        var result = await _handler.AuthenticateAsync(context);

        Assert.False(result.Succeeded);
        Assert.Null(result.AuthContext);
    }

    /// <summary>
    /// The widened filter must not swallow credentials belonging to other handlers.
    /// A non-skip result stops the chain (see AuthenticationMiddleware), so anything this
    /// handler doesn't own has to come back as Skip.
    /// </summary>
    [Theory]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIn0.sig")] // JWT -> JWT handlers
    [InlineData("noc_dGVzdHRva2VuMTIzNDU2Nzg5MA")] // direct grant -> DirectGrantTokenHandler
    [InlineData("2fd4e1c67a2d28fced849ee1bb76e7391b93eb12")] // 40-hex legacy api-secret
    [InlineData("short")]
    [InlineData("-nodashprefix")]
    [InlineData("trailingdash-")]
    [InlineData("name-short")] // digest below the 8 char minimum
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")] // 63 hex
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdefa")] // 65 hex
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeg")] // 64 chars, not hex
    public async Task AuthenticateAsync_TokenThisHandlerDoesNotOwn_ReturnsSkip(string token)
    {
        var context = CreateHttpContext();
        context.Request.QueryString = new QueryString($"?token={token}");

        var result = await _handler.AuthenticateAsync(context);

        Assert.True(result.ShouldSkip);
    }

    /// <summary>
    /// A well-formed token with no matching subject must also Skip, so a request that carries
    /// a stale ?token= alongside a valid api-secret still reaches ApiKeyHandler.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WellFormedButUnknownToken_ReturnsSkip()
    {
        var context = CreateHttpContext();
        context.Request.QueryString = new QueryString(
            "?token=00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff");

        var result = await _handler.AuthenticateAsync(context);

        Assert.True(result.ShouldSkip);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AuthenticateAsync_NoToken_ReturnsSkip()
    {
        var result = await _handler.AuthenticateAsync(CreateHttpContext());

        Assert.True(result.ShouldSkip);
    }

    [Fact]
    public void Priority_Is300()
    {
        Assert.Equal(300, _handler.Priority);
    }

    [Fact]
    public void Name_IsAccessTokenHandler()
    {
        Assert.Equal("AccessTokenHandler", _handler.Name);
    }

    private async Task<(Guid SubjectId, string Token)> MintDeviceSubjectAsync(string name)
    {
        var result = await _subjectService.CreateSubjectAsync(new Subject
        {
            Name = name,
            Type = SubjectType.Device,
            IsActive = true,
        });

        Assert.NotNull(result.AccessToken);
        return (result.Subject.Id, result.AccessToken!);
    }

    private async Task<Guid> SeedSubjectWithTokenAsync(string name, string plainToken)
    {
        var subjectId = Guid.CreateVersion7();
        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = subjectId,
            Name = name,
            IsActive = true,
            AccessTokenHash = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(plainToken))),
            AccessTokenPrefix = $"{name}-{plainToken[..8]}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        return subjectId;
    }

    private DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Items["TenantContext"] = new TenantContext(_testTenantId, "default", "Default", true);
        return context;
    }
}
