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
    IOptions<GitHubTranslationOptions> options,
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
        catch (TranslationContributionRejectedException ex)
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
        catch (TranslationContributionRejectedException ex)
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
        if (!GitHubContentService.AllowedPathPattern().IsMatch(request.Path))
            return Problem(detail: "Path must be a portal blog or docs .svx file", statusCode: 400, title: "Bad Request");

        if (string.IsNullOrWhiteSpace(request.Content)
            || System.Text.Encoding.UTF8.GetByteCount(request.Content) > MaxContentBytes)
            return Problem(detail: "Content is required and must be under 512 KB", statusCode: 400, title: "Bad Request");

        if (string.IsNullOrWhiteSpace(request.Title)
            || request.Title.Length > MaxTitleLength
            || request.Title.Any(char.IsControl))
            return Problem(detail: "Title is required, must be under 200 characters, and cannot contain control characters", statusCode: 400, title: "Bad Request");

        // Contributor identity ends up in the commit message (Co-authored-by
        // trailer) and PR body; same constraints as translation contributions.
        if (string.IsNullOrWhiteSpace(request.Contributor.Name)
            || request.Contributor.Name.Length > 128
            || request.Contributor.Name.Any(char.IsControl))
            return Problem(detail: "Contributor name is required, must be under 128 characters, and cannot contain control characters", statusCode: 400, title: "Bad Request");

        if (request.Contributor.GitHubUsername is { Length: > 0 } username
            && !System.Text.RegularExpressions.Regex.IsMatch(username, "^[A-Za-z0-9](?:-?[A-Za-z0-9]){0,38}$"))
            return Problem(detail: "Invalid GitHub username", statusCode: 400, title: "Bad Request");

        if (request.Contributor.Email is { Length: > 0 } email
            && (email.Length > 254 || !System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^\s<>@]+@[^\s<>@]+\.[^\s<>@]+$")))
            return Problem(detail: "Invalid contributor email", statusCode: 400, title: "Bad Request");

        if (request.Note?.Length > 2000)
            return Problem(detail: "Note must be under 2000 characters", statusCode: 400, title: "Bad Request");

        return null;
    }
}
