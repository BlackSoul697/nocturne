using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Nocturne.API.Services;
using Nocturne.Core.Contracts.Content;
using Nocturne.Core.Models.Content;
using OpenApi.Remote.Attributes;

namespace Nocturne.API.Controllers.V4.Platform;

[ApiController]
[Authorize]
[Route("api/v4/content")]
public class ContentContributionsController(
    IContentContributionService contentService,
    IOptions<GitHubContributionOptions> options,
    ILogger<ContentContributionsController> logger) : ControllerBase
{
    private const int MaxContentBytes = 512 * 1024;
    private const int MaxTitleLength = 200;

    [HttpPost("contributions")]
    [RemoteCommand]
    [EnableRateLimiting("translation-contributions")]
    [ProducesResponseType(typeof(ContentContributionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ContentContributionResponse>> SubmitContribution(
        [FromBody] ContentContributionRequest request, CancellationToken ct)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            return validationError;

        try
        {
            var result = contentService.HasLocalPat
                ? await contentService.SubmitAsync(request, ct)
                : await contentService.RelayAsync(request, ct);

            return StatusCode(201, result);
        }
        catch (ContributionRejectedException ex)
        {
            return Problem(detail: ex.Message, statusCode: 422, title: "Unprocessable Entity");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit content contribution");
            return Problem(detail: "Failed to submit the contribution. Try again later.",
                statusCode: 502, title: "Bad Gateway");
        }
    }

    /// <summary>
    /// Anonymous ingress for contributions relayed from instances or tools
    /// without their own PAT (the nocturne.run side of the relay). Hidden
    /// unless this instance explicitly opted in and can open PRs itself.
    /// </summary>
    [HttpPost("relay")]
    [AllowAnonymous]
    [EnableRateLimiting("translation-contributions")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<ContentContributionResponse>> AcceptRelayedContribution(
        [FromBody] ContentContributionRequest request, CancellationToken ct)
    {
        if (!options.Value.AcceptRelayedContributions || !contentService.HasLocalPat)
            return NotFound();

        var validationError = Validate(request);
        if (validationError is not null)
            return validationError;

        try
        {
            var result = await contentService.SubmitAsync(request, ct);
            return StatusCode(201, result);
        }
        catch (ContributionRejectedException ex)
        {
            return Problem(detail: ex.Message, statusCode: 422, title: "Unprocessable Entity");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit relayed content contribution");
            return Problem(detail: "Failed to submit the contribution. Try again later.",
                statusCode: 502, title: "Bad Gateway");
        }
    }

    private ObjectResult? Validate(ContentContributionRequest request)
    {
        // Bound the path before the regex sees it: the allowlist accepts
        // arbitrarily long repeated segments, and the value is echoed into a
        // branch name, a commit message and a PR body.
        if (request.Path.Length > ContributionValidation.MaxPathLength
            || !GitHubContentService.AllowedPathPattern().IsMatch(request.Path))
            return Problem(detail: "Path must be a portal blog or docs .svx file", statusCode: 400, title: "Bad Request");

        if (string.IsNullOrWhiteSpace(request.Content)
            || System.Text.Encoding.UTF8.GetByteCount(request.Content) > MaxContentBytes)
            return Problem(detail: "Content is required and must be under 512 KB", statusCode: 400, title: "Bad Request");

        if (string.IsNullOrWhiteSpace(request.Title)
            || request.Title.Length > MaxTitleLength
            || request.Title.Any(char.IsControl))
            return Problem(detail: $"Title is required, must be under {MaxTitleLength} characters, and cannot contain control characters", statusCode: 400, title: "Bad Request");

        return ContributionValidation.ValidateContributor(request.Contributor, request.Note) is { } reason
            ? Problem(detail: reason, statusCode: 400, title: "Bad Request")
            : null;
    }
}
