using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts.Translations;
using Nocturne.Core.Models.Translations;

namespace Nocturne.API.Services;

public class GitHubTranslationOptions
{
    /// <summary>
    /// PAT with contents+pull-request write access. Needs more privilege than
    /// IssuesPat, so it is a separate key; instances without one relay to
    /// nocturne.run like the support-issue flow.
    /// </summary>
    public string? TranslationsPat { get; set; }
    public string TranslationsRelayUrl { get; set; } = "https://nocturne.run/api/v4/translations/relay";
    public string ContentRelayUrl { get; set; } = "https://nocturne.run/api/v4/content/relay";
    /// <summary>
    /// Accept anonymous relayed contributions from other instances (the
    /// nocturne.run side of the relay). Requires TranslationsPat. Off by
    /// default so a regular instance never exposes an anonymous endpoint.
    /// </summary>
    public bool AcceptRelayedContributions { get; set; }
    public string Owner { get; set; } = GitHubApi.DefaultOwner;
    public string Repo { get; set; } = GitHubApi.DefaultRepo;
    public string BaseBranch { get; set; } = "main";
    public string CatalogDir { get; set; } = "src/Web/locales";
}

/// <summary>
/// Mirrors GitHubIssueService — keep the two in step.
/// </summary>
public class GitHubTranslationService(
    GitHubPrClient prClient,
    IHttpClientFactory httpClientFactory,
    IOptions<GitHubTranslationOptions> options,
    ILogger<GitHubTranslationService> logger) : ITranslationContributionService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public bool HasLocalPat => !string.IsNullOrEmpty(options.Value.TranslationsPat);

    public bool AcceptsRelay => options.Value.AcceptRelayedContributions && HasLocalPat;

    public async Task<TranslationContributionResponse> SubmitAsync(
        TranslationContributionRequest request, CancellationToken ct)
    {
        var opts = options.Value;
        using var client = prClient.CreateClient(opts.TranslationsPat);

        // The contents API caps files at 1 MB; the largest catalog is ~0.9 MB
        // today. If catalogs outgrow that, switch to the blobs API.
        var catalogPath = $"{opts.CatalogDir}/{request.Locale}.po";
        var catalogFile = await prClient.GetFileAsync(client, opts.Owner, opts.Repo, catalogPath, opts.BaseBranch, ct)
            ?? throw new TranslationContributionRejectedException($"No catalog exists for this locale ({catalogPath}).");
        var (catalogText, fileSha) = catalogFile;

        var edits = request.Entries.ToDictionary(
            e => (e.Context ?? "", e.MsgId),
            e => (IReadOnlyList<string>)e.Translations);
        var result = PoCatalogEditor.ApplyTranslations(catalogText, edits);

        if (result.Applied == 0)
            throw new TranslationContributionRejectedException(
                "No contributed message matched the current catalog. The catalog may have changed; refresh and try again.");

        var branch = $"translations/{request.Locale}-{Guid.NewGuid().ToString("N")[..12]}";
        var baseSha = await prClient.GetBranchShaAsync(client, opts.Owner, opts.Repo, opts.BaseBranch, ct);
        await prClient.CreateBranchAsync(client, opts.Owner, opts.Repo, branch, baseSha, ct);

        int prNumber;
        string prUrl;
        try
        {
            await prClient.CommitFileAsync(
                client, opts.Owner, opts.Repo, catalogPath, branch,
                fileSha, result.Text, BuildCommitMessage(request, result.Applied), ct);
            (prNumber, prUrl) = await prClient.OpenPullRequestAsync(
                client, opts.Owner, opts.Repo, branch, opts.BaseBranch,
                $"i18n({request.Locale}): {result.Applied} translation{(result.Applied == 1 ? "" : "s")} via in-app contribution",
                BuildPrBody(request, result), ct);
        }
        catch
        {
            await prClient.TryDeleteBranchAsync(client, opts.Owner, opts.Repo, branch);
            throw;
        }

        logger.LogInformation(
            "Opened translation PR #{PrNumber} for {Locale}: {Applied} applied, {Unmatched} unmatched",
            prNumber, request.Locale, result.Applied, result.Unmatched.Count);

        return new TranslationContributionResponse
        {
            PrNumber = prNumber,
            PrUrl = prUrl,
            Applied = result.Applied,
            Unmatched = [.. result.Unmatched],
        };
    }

    public async Task<TranslationContributionResponse> RelayAsync(
        TranslationContributionRequest request, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(options.Value.TranslationsRelayUrl, request, JsonOpts, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Translation relay error: {StatusCode} {Error}", response.StatusCode, error);
            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
                throw new TranslationContributionRejectedException(
                    "No contributed message matched the current catalog. The catalog may have changed; refresh and try again.");
            throw new InvalidOperationException($"Translation relay error: {response.StatusCode}");
        }

        return await response.Content.ReadFromJsonAsync<TranslationContributionResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Failed to deserialize relay response");
    }


    internal static string SanitizeMetadata(string value) => GitHubPrClient.SanitizeMetadata(value);

    internal static string RenderName(string name, bool markdown) =>
        GitHubPrClient.RenderName(name, markdown);

    /// <summary>
    /// Renders free-text contributor input inside a fenced code block.
    /// Nothing inside a code fence is interpreted, which neutralizes the
    /// references <see cref="RenderName"/> describes plus block markup and
    /// raw HTML at once — provided the note cannot close the fence, so the
    /// fence runs one backtick longer than the longest backtick run in it.
    /// </summary>
    internal static string RenderNoteAsCodeFence(string note)
    {
        var text = StripControlChars(note).ReplaceLineEndings("\n");
        var fence = new string('`', Math.Max(3, LongestBacktickRun(text) + 1));

        var sb = new StringBuilder();
        sb.AppendLine(fence);
        foreach (var line in text.Split('\n'))
            sb.AppendLine(line);
        sb.AppendLine(fence);
        return sb.ToString();
    }

    private static int LongestBacktickRun(string value)
    {
        int longest = 0, run = 0;
        foreach (var c in value)
        {
            run = c == '`' ? run + 1 : 0;
            if (run > longest)
                longest = run;
        }
        return longest;
    }

    /// <summary>Drops C0/C1 control characters but keeps line structure.</summary>
    private static string StripControlChars(string value) =>
        new([.. value.Where(c => !char.IsControl(c) || c is '\r' or '\n')]);

    internal static string BuildCommitMessage(TranslationContributionRequest request, int applied)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"chore(i18n): {request.Locale} translations via in-app contribution");
        sb.AppendLine();
        sb.AppendLine($"Applies {applied} message{(applied == 1 ? "" : "s")} contributed by {RenderName(request.Contributor.Name, markdown: false)}.");

        var coAuthor = CoAuthorTrailer(request.Contributor);
        if (coAuthor is not null)
        {
            sb.AppendLine();
            sb.AppendLine(coAuthor);
        }

        return sb.ToString();
    }

    internal static string? CoAuthorTrailer(TranslationContributorDto contributor) =>
        GitHubPrClient.CoAuthorTrailer(contributor);

    internal static string BuildPrBody(TranslationContributionRequest request, PoEditResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Translation contribution for `{request.Locale}` submitted through the in-app translation mode.");
        sb.AppendLine();
        sb.AppendLine($"- **Contributor:** {RenderName(request.Contributor.Name, markdown: true)}"
            + (string.IsNullOrWhiteSpace(request.Contributor.GitHubUsername)
                ? ""
                : $" (@{SanitizeMetadata(request.Contributor.GitHubUsername)})"));
        sb.AppendLine($"- **Messages updated:** {result.Applied}");

        if (result.Unmatched.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<details>");
            sb.AppendLine($"<summary>{result.Unmatched.Count} entr{(result.Unmatched.Count == 1 ? "y" : "ies")} no longer in the catalog (skipped)</summary>");
            sb.AppendLine();
            // Backticks are dropped rather than escaped: a backslash is literal
            // inside a CommonMark code span, so an escaped backtick would still
            // close it.
            foreach (var entry in result.Unmatched.Take(50))
            {
                var display = entry.MsgId.Replace("\r", "").Replace("\n", "\\n").Replace("`", "");
                if (display.Length > 120)
                    display = display[..120] + "…";
                var context = entry.Context.Length == 0
                    ? ""
                    : $" (context: {SanitizeMetadata(entry.Context)})";
                sb.AppendLine($"- `{display}`{context}");
            }
            sb.AppendLine();
            sb.AppendLine("</details>");
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            sb.AppendLine();
            sb.AppendLine("## Contributor note");
            sb.AppendLine();
            sb.Append(RenderNoteAsCodeFence(request.Note));
        }

        return sb.ToString();
    }


}

public class TranslationContributionRejectedException(string message) : Exception(message);
