using Nocturne.API.Services;
using Nocturne.Core.Models.Content;
using Nocturne.Core.Models.Translations;

namespace Nocturne.API.Tests.Services;

public class GitHubContentServiceTests
{
    [Theory]
    [InlineData("src/Web/packages/portal/src/content/blog/my-post.svx", true)]
    [InlineData("src/Web/packages/portal/src/content/docs/authentication/github.svx", true)]
    [InlineData("src/Web/packages/portal/src/content/docs/windows-widget.svx", true)]
    [InlineData("src/Web/packages/portal/src/content/blog/post.2024.svx", true)]
    [InlineData("src/Web/packages/portal/src/content/blog/../../../../API/Program.cs", false)]
    [InlineData("src/Web/packages/portal/src/content/blog/post.md", false)]
    [InlineData("src/Web/packages/portal/src/content/email/steal.svx", false)]
    [InlineData("src/API/Nocturne.API/Program.cs", false)]
    [InlineData(".github/workflows/deploy.yml", false)]
    [InlineData("src/Web/packages/portal/src/content/blog/UPPER.svx", false)]
    [InlineData("src/Web/packages/portal/src/content/blog/.hidden.svx", false)]
    [InlineData("src/Web/packages/portal/src/content/blog//double.svx", false)]
    public void AllowedPathPattern_Constrains_To_Portal_Content(string path, bool allowed)
    {
        GitHubContentService.AllowedPathPattern().IsMatch(path).Should().Be(allowed);
    }

    private static ContentContributionRequest Request() => new()
    {
        Path = "src/Web/packages/portal/src/content/blog/my-post.svx",
        Content = "---\ntitle: My Post\n---\n\nBody",
        Title = "My Post",
        Contributor = new TranslationContributorDto { Name = "Jane Doe", GitHubUsername = "janedoe" },
        Note = "First draft",
    };

    [Fact]
    public void CommitMessage_Includes_Slug_Attribution_And_Trailer()
    {
        var message = GitHubContentService.BuildCommitMessage(Request(), created: true);

        message.Should().StartWith("content: add my-post");
        message.Should().Contain("Contributed by Jane Doe");
        message.Should().Contain("Co-authored-by: janedoe <janedoe@users.noreply.github.com>");
    }

    [Fact]
    public void PrBody_Lists_File_Contributor_And_Note()
    {
        var body = GitHubContentService.BuildPrBody(Request(), created: false);

        body.Should().StartWith("Updated content");
        body.Should().Contain("`src/Web/packages/portal/src/content/blog/my-post.svx`");
        body.Should().Contain("**Contributor:** Jane Doe (@janedoe)");
        body.Should().Contain("First draft");
    }

    [Fact]
    public void PrBody_Removes_Url_And_Shorthand_References_From_The_Contributor_Name()
    {
        var request = Request() with
        {
            Contributor = new TranslationContributorDto
            {
                Name = "Jane fixes GH-123 see https://github.com/nightscout/nocturne/issues/456",
            },
        };

        var body = GitHubContentService.BuildPrBody(request, created: true);

        // Neither form carries a "#" or "@", so the escapes miss both: they
        // would backlink at PR-open and auto-close on merge.
        body.Should().Contain("**Contributor:** Jane fixes GH 123 see");
        body.Should().NotContain("GH-123");
        body.Should().NotContain("github.com");
    }

    [Fact]
    public void CommitMessage_Removes_Url_And_Shorthand_References_From_The_Contributor_Name()
    {
        var request = Request() with
        {
            Contributor = new TranslationContributorDto
            {
                Name = "Jane htt#ps://github.com/nightscout/nocturne/issues/9 GH#-7",
                Email = "jane@example.com",
            },
        };

        var message = GitHubContentService.BuildCommitMessage(request, created: true);

        // Dropping "#" reassembles both forms, so they have to be neutralised
        // after that pass. The co-author trailer is a commit-message sink too.
        message.Should().Contain("Contributed by Jane  GH 7 via the in-app content studio.");
        message.Should().Contain("Co-authored-by: Jane  GH 7 <jane@example.com>");
        message.Should().NotContain("github.com");
        message.Should().NotContain("GH-7");
    }

    [Fact]
    public void CommitMessage_Sanitizes_Injection_Attempts()
    {
        var request = Request() with
        {
            Contributor = new TranslationContributorDto
            {
                Name = "Jane\nSigned-off-by: maintainer <m@x>",
            },
        };

        var message = GitHubContentService.BuildCommitMessage(request, created: false);

        var lines = message.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.Should().NotContain(l => l.StartsWith("Signed-off-by"));
    }
}
