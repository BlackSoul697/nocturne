using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

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
    /// <summary>
    /// Accept anonymous relayed contributions from other instances (the
    /// nocturne.run side of the relay). Requires TranslationsPat. Off by
    /// default so a regular instance never exposes an anonymous endpoint.
    /// </summary>
    public bool AcceptRelayedContributions { get; set; }
    public string Owner { get; set; } = "nightscout";
    public string Repo { get; set; } = "nocturne";
    public string BaseBranch { get; set; } = "main";
    public string CatalogDir { get; set; } = "src/Web/locales";
}

public record TranslationEntryDto
{
    public required string MsgId { get; init; }
    public string? Context { get; init; }
    /// <summary>One value for singular messages, nplurals values for plural ones.</summary>
    public required List<string> Translations { get; init; }
}

public record TranslationContributorDto
{
    public required string Name { get; init; }
    public string? GitHubUsername { get; init; }
    public string? Email { get; init; }
}

public record TranslationContributionRequest
{
    public required string Locale { get; init; }
    public required List<TranslationEntryDto> Entries { get; init; }
    public required TranslationContributorDto Contributor { get; init; }
    public string? Note { get; init; }
}

public record TranslationContributionResponse
{
    public int PrNumber { get; init; }
    public string PrUrl { get; init; } = "";
    public int Applied { get; init; }
    public List<string> Unmatched { get; init; } = [];
}

/// <summary>
/// Turns an in-app translation contribution into an upstream pull request:
/// fetch the locale catalog from the repo, apply the contributed msgstr
/// values, commit to a new branch and open a PR with contributor attribution.
/// Instances without a PAT relay the contribution to nocturne.run, mirroring
/// GitHubIssueService.
/// </summary>
public class GitHubTranslationService(
    IHttpClientFactory httpClientFactory,
    IOptions<GitHubTranslationOptions> options,
    ILogger<GitHubTranslationService> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public bool HasLocalPat => !string.IsNullOrEmpty(options.Value.TranslationsPat);

    public async Task<TranslationContributionResponse> SubmitAsync(
        TranslationContributionRequest request, CancellationToken ct)
    {
        var opts = options.Value;
        using var client = CreateGitHubClient();

        var catalogPath = $"{opts.CatalogDir}/{request.Locale}.po";
        var (catalogText, fileSha) = await GetCatalogAsync(client, catalogPath, ct);

        var edits = request.Entries.ToDictionary(
            e => (e.Context ?? "", e.MsgId),
            e => (IReadOnlyList<string>)e.Translations);
        var result = PoCatalogEditor.ApplyTranslations(catalogText, edits);

        if (result.Applied == 0)
            throw new TranslationContributionRejectedException(
                "No contributed message matched the current catalog. The catalog may have changed; refresh and try again.");

        var branch = $"translations/{request.Locale}-{Guid.NewGuid().ToString("N")[..12]}";
        var baseSha = await GetBranchShaAsync(client, opts.BaseBranch, ct);
        await CreateBranchAsync(client, branch, baseSha, ct);

        int prNumber;
        string prUrl;
        try
        {
            await CommitCatalogAsync(client, catalogPath, branch, fileSha, result.Text, request, result.Applied, ct);
            (prNumber, prUrl) = await OpenPullRequestAsync(client, branch, request, result, ct);
        }
        catch
        {
            await TryDeleteBranchAsync(client, branch);
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

    private async Task<(string Text, string Sha)> GetCatalogAsync(
        HttpClient client, string path, CancellationToken ct)
    {
        var opts = options.Value;
        // The contents API caps files at 1 MB; the largest catalog is ~0.9 MB
        // today. If catalogs outgrow that, switch to the blobs API.
        var response = await client.GetAsync(
            $"/repos/{opts.Owner}/{opts.Repo}/contents/{path}?ref={Uri.EscapeDataString(opts.BaseBranch)}", ct);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new TranslationContributionRejectedException($"No catalog exists for this locale ({path}).");
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("GitHub API error fetching catalog: {StatusCode} {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"GitHub API error: {response.StatusCode}");
        }

        var file = await response.Content.ReadFromJsonAsync<GitHubContentResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub content response");
        var text = Encoding.UTF8.GetString(Convert.FromBase64String(file.Content.Replace("\n", "")));
        return (text, file.Sha);
    }

    private async Task<string> GetBranchShaAsync(HttpClient client, string branch, CancellationToken ct)
    {
        var opts = options.Value;
        var response = await client.GetAsync(
            $"/repos/{opts.Owner}/{opts.Repo}/git/ref/heads/{branch}", ct);
        response.EnsureSuccessStatusCode();
        var reference = await response.Content.ReadFromJsonAsync<GitHubRefResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub ref response");
        return reference.Object.Sha;
    }

    private async Task CreateBranchAsync(HttpClient client, string branch, string sha, CancellationToken ct)
    {
        var opts = options.Value;
        var response = await client.PostAsJsonAsync(
            $"/repos/{opts.Owner}/{opts.Repo}/git/refs",
            new { @ref = $"refs/heads/{branch}", sha }, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("GitHub API error creating branch: {StatusCode} {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"GitHub API error: {response.StatusCode}");
        }
    }

    private async Task CommitCatalogAsync(
        HttpClient client, string path, string branch, string fileSha,
        string newText, TranslationContributionRequest request, int applied, CancellationToken ct)
    {
        var opts = options.Value;
        var message = BuildCommitMessage(request, applied);

        var response = await client.PutAsJsonAsync(
            $"/repos/{opts.Owner}/{opts.Repo}/contents/{path}",
            new
            {
                message,
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes(newText)),
                sha = fileSha,
                branch,
            }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("GitHub API error committing catalog: {StatusCode} {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"GitHub API error: {response.StatusCode}");
        }
    }

    private async Task<(int Number, string Url)> OpenPullRequestAsync(
        HttpClient client, string branch, TranslationContributionRequest request,
        PoEditResult result, CancellationToken ct)
    {
        var opts = options.Value;
        var response = await client.PostAsJsonAsync(
            $"/repos/{opts.Owner}/{opts.Repo}/pulls",
            new
            {
                title = $"i18n({request.Locale}): {result.Applied} translation{(result.Applied == 1 ? "" : "s")} via in-app contribution",
                head = branch,
                @base = opts.BaseBranch,
                body = BuildPrBody(request, result),
                maintainer_can_modify = true,
            }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("GitHub API error opening PR: {StatusCode} {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"GitHub API error: {response.StatusCode}");
        }

        var pr = await response.Content.ReadFromJsonAsync<GitHubPullResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub PR response");
        return (pr.Number, pr.HtmlUrl);
    }

    /// <summary>
    /// Defense in depth behind controller validation: these values land in a
    /// commit message and PR body, so newlines or angle brackets would allow
    /// trailer injection regardless of what the caller validated.
    /// </summary>
    internal static string SanitizeMetadata(string value) =>
        new([.. value.Trim().Where(c => !char.IsControl(c) && c is not '<' and not '>')]);

    internal static string BuildCommitMessage(TranslationContributionRequest request, int applied)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"chore(i18n): {request.Locale} translations via in-app contribution");
        sb.AppendLine();
        sb.AppendLine($"Applies {applied} message{(applied == 1 ? "" : "s")} contributed by {SanitizeMetadata(request.Contributor.Name)}.");

        var coAuthor = CoAuthorTrailer(request.Contributor);
        if (coAuthor is not null)
        {
            sb.AppendLine();
            sb.AppendLine(coAuthor);
        }

        return sb.ToString();
    }

    internal static string? CoAuthorTrailer(TranslationContributorDto contributor)
    {
        if (!string.IsNullOrWhiteSpace(contributor.GitHubUsername))
        {
            var username = SanitizeMetadata(contributor.GitHubUsername);
            return $"Co-authored-by: {username} <{username}@users.noreply.github.com>";
        }

        if (!string.IsNullOrWhiteSpace(contributor.Email))
            return $"Co-authored-by: {SanitizeMetadata(contributor.Name)} <{SanitizeMetadata(contributor.Email)}>";

        return null;
    }

    internal static string BuildPrBody(TranslationContributionRequest request, PoEditResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Translation contribution for `{request.Locale}` submitted through the in-app translation mode.");
        sb.AppendLine();
        sb.AppendLine($"- **Contributor:** {SanitizeMetadata(request.Contributor.Name)}"
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
            // msgids are arbitrary contributor input echoed into markdown:
            // keep them on one line inside the code span or they escape it.
            foreach (var msgId in result.Unmatched.Take(50))
            {
                var display = msgId.Replace("\r", "").Replace("\n", "\\n").Replace("`", "\\`");
                if (display.Length > 120)
                    display = display[..120] + "…";
                sb.AppendLine($"- `{display}`");
            }
            sb.AppendLine();
            sb.AppendLine("</details>");
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            sb.AppendLine();
            sb.AppendLine("## Contributor note");
            sb.AppendLine();
            sb.AppendLine(request.Note);
        }

        return sb.ToString();
    }

    private async Task TryDeleteBranchAsync(HttpClient client, string branch)
    {
        var opts = options.Value;
        try
        {
            await client.DeleteAsync(
                $"/repos/{opts.Owner}/{opts.Repo}/git/refs/heads/{branch}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up branch {Branch} after error", branch);
        }
    }

    private HttpClient CreateGitHubClient()
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.github.com");
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Nocturne", "1.0"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", options.Value.TranslationsPat);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private record GitHubContentResponse
    {
        [JsonPropertyName("content")]
        public string Content { get; init; } = "";
        [JsonPropertyName("sha")]
        public string Sha { get; init; } = "";
    }

    private record GitHubRefResponse
    {
        [JsonPropertyName("object")]
        public GitHubRefObject Object { get; init; } = new();
    }

    private record GitHubRefObject
    {
        [JsonPropertyName("sha")]
        public string Sha { get; init; } = "";
    }

    private record GitHubPullResponse
    {
        [JsonPropertyName("number")]
        public int Number { get; init; }
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = "";
    }
}

public class TranslationContributionRejectedException(string message) : Exception(message);
