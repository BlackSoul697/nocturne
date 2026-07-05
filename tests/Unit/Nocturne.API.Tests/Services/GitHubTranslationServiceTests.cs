using Nocturne.API.Services;

namespace Nocturne.API.Tests.Services;

public class GitHubTranslationServiceTests
{
    private static TranslationContributionRequest Request(
        string? gitHubUsername = null, string? email = null, string? note = null) => new()
    {
        Locale = "fr",
        Entries = [new TranslationEntryDto { MsgId = "Hello", Translations = ["Bonjour"] }],
        Contributor = new TranslationContributorDto
        {
            Name = "Jane Doe",
            GitHubUsername = gitHubUsername,
            Email = email,
        },
        Note = note,
    };

    [Fact]
    public void CoAuthorTrailer_Prefers_GitHub_Username()
    {
        var trailer = GitHubTranslationService.CoAuthorTrailer(
            Request(gitHubUsername: "janedoe", email: "jane@example.com").Contributor);

        trailer.Should().Be("Co-authored-by: janedoe <janedoe@users.noreply.github.com>");
    }

    [Fact]
    public void CoAuthorTrailer_Falls_Back_To_Email()
    {
        var trailer = GitHubTranslationService.CoAuthorTrailer(
            Request(email: "jane@example.com").Contributor);

        trailer.Should().Be("Co-authored-by: Jane Doe <jane@example.com>");
    }

    [Fact]
    public void CoAuthorTrailer_Is_Null_Without_Identity()
    {
        GitHubTranslationService.CoAuthorTrailer(Request().Contributor).Should().BeNull();
    }

    [Fact]
    public void CommitMessage_Includes_Attribution_And_Trailer()
    {
        var message = GitHubTranslationService.BuildCommitMessage(
            Request(gitHubUsername: "janedoe"), applied: 3);

        message.Should().StartWith("chore(i18n): fr translations via in-app contribution");
        message.Should().Contain("Applies 3 messages contributed by Jane Doe.");
        message.Should().Contain("Co-authored-by: janedoe");
    }

    [Fact]
    public void PrBody_Lists_Contributor_Note_And_Unmatched()
    {
        var body = GitHubTranslationService.BuildPrBody(
            Request(gitHubUsername: "janedoe", note: "Reviewed against the app UI"),
            new PoEditResult
            {
                Text = "",
                Applied = 2,
                Unmatched = ["Gone message"],
            });

        body.Should().Contain("**Contributor:** Jane Doe (@janedoe)");
        body.Should().Contain("**Messages updated:** 2");
        body.Should().Contain("`Gone message`");
        body.Should().Contain("## Contributor note");
        body.Should().Contain("Reviewed against the app UI");
    }
}
