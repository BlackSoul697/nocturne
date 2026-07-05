using Microsoft.AspNetCore.Http;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts.Translations;
using Nocturne.Core.Models.Authorization;
using Nocturne.Core.Models.Translations;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Tests.Shared.Infrastructure;

namespace Nocturne.API.Tests.Services;

public class TranslationDraftServiceTests
{
    private static readonly Guid TestSubjectId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid OtherSubjectId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly NocturneDbContext _dbContext;
    private readonly Mock<ITranslationContributionService> _contributionService = new();
    private readonly TranslationDraftService _service;

    public TranslationDraftServiceTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryContext();
        _dbContext.TenantId = TestTenantId;

        var httpContext = new DefaultHttpContext();
        httpContext.Items["AuthContext"] = new AuthContext
        {
            IsAuthenticated = true,
            SubjectId = TestSubjectId,
            TenantId = TestTenantId,
        };

        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        _service = new TranslationDraftService(_dbContext, httpContextAccessor, _contributionService.Object);
    }

    private static TranslationEntryDto Entry(string msgId, string? translation, string? context = null) => new()
    {
        MsgId = msgId,
        Context = context,
        Translations = translation is null ? [] : [translation],
    };

    private static TranslationContributorDto Contributor() => new() { Name = "Jane Doe" };

    [Fact]
    public async Task UpsertDraftsAsync_Creates_And_Updates_By_Key()
    {
        await _service.UpsertDraftsAsync("fr", [Entry("Hello", "Bonjour")]);
        var updated = await _service.UpsertDraftsAsync("fr", [Entry("Hello", "Salut")]);

        updated.Should().ContainSingle();
        updated[0].Translations.Should().Equal("Salut");
        (await _service.GetDraftsAsync("fr")).Should().ContainSingle();
    }

    [Fact]
    public async Task UpsertDraftsAsync_Distinguishes_Contexts()
    {
        var result = await _service.UpsertDraftsAsync("fr",
            [Entry("Welcome", "Bienvenue"), Entry("Welcome", "Accueil", context: "page-title")]);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpsertDraftsAsync_Empty_Translations_Deletes_Draft()
    {
        await _service.UpsertDraftsAsync("fr", [Entry("Hello", "Bonjour")]);
        var result = await _service.UpsertDraftsAsync("fr", [Entry("Hello", null)]);

        result.Should().BeEmpty();
        (await _service.GetDraftsAsync("fr")).Should().BeEmpty();
    }

    [Fact]
    public async Task Drafts_Are_Scoped_To_Subject_And_Locale()
    {
        _dbContext.TranslationDrafts.Add(new TranslationDraftEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = TestTenantId,
            SubjectId = OtherSubjectId,
            Locale = "fr",
            MsgId = "Hello",
            Translations = ["Bonjour"],
        });
        await _dbContext.SaveChangesAsync();
        await _service.UpsertDraftsAsync("de", [Entry("Hello", "Hallo")]);

        (await _service.GetDraftsAsync("fr")).Should().BeEmpty();
        (await _service.GetDraftsAsync("de")).Should().ContainSingle();
    }

    [Fact]
    public async Task ClearDraftsAsync_Removes_Only_That_Locale()
    {
        await _service.UpsertDraftsAsync("fr", [Entry("Hello", "Bonjour")]);
        await _service.UpsertDraftsAsync("de", [Entry("Hello", "Hallo")]);

        var removed = await _service.ClearDraftsAsync("fr");

        removed.Should().Be(1);
        (await _service.GetDraftsAsync("de")).Should().ContainSingle();
    }

    [Fact]
    public async Task SubmitDraftsAsync_Throws_When_No_Drafts()
    {
        var act = () => _service.SubmitDraftsAsync("fr", Contributor(), null);

        await act.Should().ThrowAsync<TranslationContributionRejectedException>();
    }

    [Fact]
    public async Task SubmitDraftsAsync_Deletes_Applied_And_Keeps_Unmatched()
    {
        await _service.UpsertDraftsAsync("fr", [Entry("Hello", "Bonjour"), Entry("Gone", "Parti")]);
        _contributionService.SetupGet(s => s.HasLocalPat).Returns(true);
        _contributionService
            .Setup(s => s.SubmitAsync(It.IsAny<TranslationContributionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranslationContributionResponse
            {
                PrNumber = 12,
                PrUrl = "https://github.com/x/y/pull/12",
                Applied = 1,
                Unmatched = ["Gone"],
            });

        var result = await _service.SubmitDraftsAsync("fr", Contributor(), null);

        result.Contribution.PrNumber.Should().Be(12);
        result.RemainingDrafts.Should().Be(1);
        var remaining = await _service.GetDraftsAsync("fr");
        remaining.Should().ContainSingle().Which.MsgId.Should().Be("Gone");
    }

    [Fact]
    public async Task SubmitDraftsAsync_Uses_Relay_Without_Pat_And_Sends_All_Drafts()
    {
        await _service.UpsertDraftsAsync("fr", [Entry("Hello", "Bonjour")]);
        _contributionService.SetupGet(s => s.HasLocalPat).Returns(false);
        TranslationContributionRequest? sent = null;
        _contributionService
            .Setup(s => s.RelayAsync(It.IsAny<TranslationContributionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TranslationContributionRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(new TranslationContributionResponse { Applied = 1 });

        await _service.SubmitDraftsAsync("fr", Contributor(), "note");

        sent.Should().NotBeNull();
        sent!.Locale.Should().Be("fr");
        sent.Entries.Should().ContainSingle().Which.MsgId.Should().Be("Hello");
        sent.Note.Should().Be("note");
        _contributionService.Verify(
            s => s.SubmitAsync(It.IsAny<TranslationContributionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        (await _service.GetDraftsAsync("fr")).Should().BeEmpty();
    }
}
