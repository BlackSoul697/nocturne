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
    public void CommitMessage_Strips_Trailer_Injection_From_Contributor_Fields()
    {
        var request = Request() with
        {
            Contributor = new TranslationContributorDto
            {
                Name = "Jane\nCo-authored-by: victim <victim@example.com>",
                Email = "jane@example.com\nSigned-off-by: maintainer <m@x>",
            },
        };

        var message = GitHubTranslationService.BuildCommitMessage(request, applied: 1);

        // Sanitization keeps injected text inert by collapsing it onto the
        // legitimate lines: no attacker-controlled line can become a trailer.
        var lines = message.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.Count(l => l.StartsWith("Co-authored-by:")).Should().Be(1);
        lines.Should().NotContain(l => l.StartsWith("Signed-off-by"));
        lines.Single(l => l.StartsWith("Co-authored-by:")).Should().NotContain("<victim@example.com>");
    }

    [Fact]
    public void SanitizeMetadata_Removes_Control_Chars_And_Angle_Brackets()
    {
        GitHubTranslationService.SanitizeMetadata("a\r\nb<c>d\te ")
            .Should().Be("abcde");
    }

    [Fact]
    public void PrBody_Keeps_Unmatched_MsgIds_On_One_Line()
    {
        var body = GitHubTranslationService.BuildPrBody(
            Request(),
            new PoEditResult
            {
                Text = "",
                Applied = 1,
                Unmatched = ["evil\n</details>\n# heading"],
            });

        body.Should().NotContain("evil\n");
        body.Should().Contain("evil\\n</details>\\n# heading");
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
        body.Should().Contain("> Reviewed against the app UI");
    }

    [Fact]
    public void PrBody_Renders_Note_As_An_Inert_Blockquote()
    {
        var body = GitHubTranslationService.BuildPrBody(
            Request(note: "cc @nightscout/maintainers\nFixes #1234\n`rm -rf`\n\n## Injected heading"),
            new PoEditResult { Text = "", Applied = 1, Unmatched = [] });

        // Every note line is quoted, so no line can become a heading, a list
        // or a bare "Fixes #N" that GitHub would act on.
        var noteLines = body[body.IndexOf("## Contributor note", StringComparison.Ordinal)..]
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Skip(2)
            .Where(l => l.Length > 0)
            .ToList();
        noteLines.Should().OnlyContain(l => l.StartsWith('>'));

        // No bare mention or issue reference survives: every "@" and "#" in the
        // note is backslash-escaped.
        const string heading = "## Contributor note";
        var note = body[(body.IndexOf(heading, StringComparison.Ordinal) + heading.Length)..];
        for (var i = 0; i < note.Length; i++)
            if (note[i] is '@' or '#' or '`')
                note[i - 1].Should().Be('\\');

        body.Should().Contain(@"\@nightscout/maintainers");
        body.Should().Contain(@"Fixes \#1234");
        body.Should().Contain(@"\`rm -rf\`");
        body.Should().NotContain("\n## Injected heading");
    }

    [Fact]
    public void PrBody_Strips_Control_Chars_From_The_Note()
    {
        var body = GitHubTranslationService.BuildPrBody(
            Request(note: "clean\u0000er\u001b[31m text"),
            new PoEditResult { Text = "", Applied = 1, Unmatched = [] });

        body.Should().Contain("> cleaner[31m text");
        body.Should().NotContain("\u0000");
        body.Should().NotContain("\u001b");
    }

    [Fact]
    public void PrBody_Escapes_Backslashes_Before_Markdown_Actives()
    {
        var body = GitHubTranslationService.BuildPrBody(
            Request(note: @"trailing backslash \@nobody"),
            new PoEditResult { Text = "", Applied = 1, Unmatched = [] });

        // The submitted backslash is escaped first, so it cannot neutralise
        // the escape we add in front of the "@".
        body.Should().Contain(@"trailing backslash \\\@nobody");
    }

    [Fact]
    public void PrBody_Removes_Backticks_From_Unmatched_MsgIds()
    {
        var body = GitHubTranslationService.BuildPrBody(
            Request(),
            new PoEditResult
            {
                Text = "",
                Applied = 1,
                // A backslash is literal inside a CommonMark code span, so an
                // escaped backtick would still terminate the span.
                Unmatched = ["evil` <img src=x> `rest"],
            });

        body.Should().Contain("`evil <img src=x> rest`");
        body.Should().NotContain(@"\`");
    }
}
