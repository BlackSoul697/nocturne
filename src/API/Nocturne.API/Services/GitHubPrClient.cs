using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Nocturne.Core.Models.Translations;

namespace Nocturne.API.Services;

/// <summary>
/// Shared GitHub REST plumbing for contribution flows that open pull
/// requests (translations, CMS content): fetch a file, branch, commit, open
/// the PR, and clean up on failure.
/// </summary>
public partial class GitHubPrClient(IHttpClientFactory httpClientFactory, ILogger<GitHubPrClient> logger)
{
    public HttpClient CreateClient(string? pat) => GitHubApi.CreateClient(httpClientFactory, pat);

    /// <summary>Returns null when the file does not exist on the ref.</summary>
    public async Task<(string Text, string Sha)?> GetFileAsync(
        HttpClient client, string owner, string repo, string path, string reference, CancellationToken ct)
    {
        // The contents API caps files at 1 MB; large files need the blobs API.
        var response = await client.GetAsync(
            $"/repos/{owner}/{repo}/contents/{path}?ref={Uri.EscapeDataString(reference)}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("GitHub API error fetching {Path}: {StatusCode} {Error}", path, response.StatusCode, error);
            throw new InvalidOperationException($"GitHub API error: {response.StatusCode}");
        }

        var file = await response.Content.ReadFromJsonAsync<GitHubContentResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub content response");
        var text = Encoding.UTF8.GetString(Convert.FromBase64String(file.Content.Replace("\n", "")));
        return (text, file.Sha);
    }

    public async Task<string> GetBranchShaAsync(
        HttpClient client, string owner, string repo, string branch, CancellationToken ct)
    {
        var response = await client.GetAsync($"/repos/{owner}/{repo}/git/ref/heads/{branch}", ct);
        response.EnsureSuccessStatusCode();
        var reference = await response.Content.ReadFromJsonAsync<GitHubRefResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize GitHub ref response");
        return reference.Object.Sha;
    }

    public async Task CreateBranchAsync(
        HttpClient client, string owner, string repo, string branch, string sha, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(
            $"/repos/{owner}/{repo}/git/refs",
            new { @ref = $"refs/heads/{branch}", sha }, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("GitHub API error creating branch: {StatusCode} {Error}", response.StatusCode, error);
            throw new InvalidOperationException($"GitHub API error: {response.StatusCode}");
        }
    }

    /// <summary>fileSha null creates the file; non-null updates it.</summary>
    public async Task CommitFileAsync(
        HttpClient client, string owner, string repo, string path, string branch,
        string? fileSha, string content, string message, CancellationToken ct)
    {
        var response = await client.PutAsJsonAsync(
            $"/repos/{owner}/{repo}/contents/{path}",
            new
            {
                message,
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
                sha = fileSha,
                branch,
            }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("GitHub API error committing {Path}: {StatusCode} {Error}", path, response.StatusCode, error);
            throw new InvalidOperationException($"GitHub API error: {response.StatusCode}");
        }
    }

    public async Task<(int Number, string Url)> OpenPullRequestAsync(
        HttpClient client, string owner, string repo, string branch, string baseBranch,
        string title, string body, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync(
            $"/repos/{owner}/{repo}/pulls",
            new
            {
                title,
                head = branch,
                @base = baseBranch,
                body,
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

    public async Task TryDeleteBranchAsync(HttpClient client, string owner, string repo, string branch)
    {
        try
        {
            await client.DeleteAsync($"/repos/{owner}/{repo}/git/refs/heads/{branch}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up branch {Branch} after error", branch);
        }
    }

    /// <summary>
    /// Defense in depth behind controller validation: these values land in
    /// commit messages and PR bodies, so newlines or angle brackets would
    /// allow trailer injection regardless of what the caller validated.
    /// </summary>
    public static string SanitizeMetadata(string value) =>
        new([.. value.Trim().Where(c => !char.IsControl(c) && c is not '<' and not '>')]);

    /// <summary>
    /// Renders a contributor-supplied display name for a sink GitHub gives
    /// side effects to. The name arrives from an anonymous relay, so
    /// <c>Jane fixes #12 cc @someone</c> would otherwise auto-close an issue
    /// and notify arbitrary users from the upstream PR body and from the
    /// commit message. A commit message is not markdown — a backslash escape
    /// would render literally there — so the reference-carrying characters
    /// are dropped instead of escaped when <paramref name="markdown"/> is
    /// false. The backslash is escaped first so a submitted <c>\</c> cannot
    /// consume the escape that follows it.
    ///
    /// <c>#</c> handling covers <c>#12</c> and <c>owner/repo#12</c>, but
    /// GitHub resolves two further reference forms that carry no <c>#</c> and
    /// no <c>@</c>: the <c>GH-12</c> shorthand and a full issue or pull URL.
    /// Both fit inside a name and both honour closing keywords, so both are
    /// removed outright — a person's name legitimately contains neither.
    /// </summary>
    public static string RenderName(string name, bool markdown)
    {
        var value = SanitizeMetadata(name);
        value = markdown
            ? value.Replace("\\", "\\\\").Replace("@", "\\@").Replace("#", "\\#").Replace("`", "\\`")
            : new string([.. value.Where(c => c is not '@' and not '#')]);

        // Last, because dropping a "#" above can splice a reference back
        // together ("htt#ps://…", "GH#-1"). Neither pass can recreate the
        // other's target: URL removal only deletes, and separating "GH" from
        // its digits cannot produce a "://".
        value = UrlReference().Replace(value, "");
        return GitHubShorthandReference().Replace(value, "$1 ").Trim();
    }

    /// <summary>Any absolute http(s) URL, which covers the full issue/PR reference form.</summary>
    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlReference();

    /// <summary>
    /// The <c>GH-123</c> shorthand. Only the hyphen is replaced: the autolink
    /// requires <c>GH-</c> immediately followed by a digit, so a space between
    /// them leaves nothing for GitHub to resolve while the name stays readable.
    /// </summary>
    [GeneratedRegex(@"(GH)-(?=\d)", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubShorthandReference();

    public static string? CoAuthorTrailer(TranslationContributorDto contributor)
    {
        if (!string.IsNullOrWhiteSpace(contributor.GitHubUsername))
        {
            var username = SanitizeMetadata(contributor.GitHubUsername);
            return $"Co-authored-by: {username} <{username}@users.noreply.github.com>";
        }

        // The trailer lands in a commit message, so the name gets the same
        // reference-dropping treatment as the attribution line above it.
        if (!string.IsNullOrWhiteSpace(contributor.Email))
            return $"Co-authored-by: {RenderName(contributor.Name, markdown: false)} <{SanitizeMetadata(contributor.Email)}>";

        return null;
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
