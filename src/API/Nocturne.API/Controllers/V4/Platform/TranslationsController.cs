using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Nocturne.API.Services;
using OpenApi.Remote.Attributes;

namespace Nocturne.API.Controllers.V4.Platform;

[ApiController]
[Authorize]
[Route("api/v4/translations")]
public partial class TranslationsController(
    GitHubTranslationService translationService,
    IOptions<GitHubTranslationOptions> options,
    ILogger<TranslationsController> logger) : ControllerBase
{
    private const int MaxEntries = 500;
    private const int MaxMsgIdLength = 4096;
    private const int MaxTranslationLength = 8192;
    private const int MaxPluralForms = 8;

    [GeneratedRegex("^[a-z]{2,3}(-[A-Za-z0-9]{2,8})?$")]
    private static partial Regex LocalePattern();

    [HttpPost("contributions")]
    [RemoteCommand]
    [EnableRateLimiting("translation-contributions")]
    [ProducesResponseType(typeof(TranslationContributionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<TranslationContributionResponse>> SubmitContribution(
        [FromBody] TranslationContributionRequest request, CancellationToken ct)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            return validationError;

        try
        {
            var result = translationService.HasLocalPat
                ? await translationService.SubmitAsync(request, ct)
                : await translationService.RelayAsync(request, ct);

            return StatusCode(201, result);
        }
        catch (TranslationContributionRejectedException ex)
        {
            return Problem(detail: ex.Message, statusCode: 422, title: "Unprocessable Entity");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit translation contribution");
            return Problem(detail: "Failed to submit the contribution. Try again later.",
                statusCode: 502, title: "Bad Gateway");
        }
    }

    /// <summary>
    /// Anonymous ingress for contributions relayed from instances without
    /// their own PAT (the nocturne.run side of the relay). Hidden unless this
    /// instance explicitly opted in and can open PRs itself. The relayed
    /// payload is re-validated here; the rate limit is shared with the
    /// authenticated endpoint.
    /// </summary>
    [HttpPost("relay")]
    [AllowAnonymous]
    [EnableRateLimiting("translation-contributions")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<TranslationContributionResponse>> AcceptRelayedContribution(
        [FromBody] TranslationContributionRequest request, CancellationToken ct)
    {
        if (!options.Value.AcceptRelayedContributions || !translationService.HasLocalPat)
            return NotFound();

        var validationError = Validate(request);
        if (validationError is not null)
            return validationError;

        try
        {
            var result = await translationService.SubmitAsync(request, ct);
            return StatusCode(201, result);
        }
        catch (TranslationContributionRejectedException ex)
        {
            return Problem(detail: ex.Message, statusCode: 422, title: "Unprocessable Entity");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit relayed translation contribution");
            return Problem(detail: "Failed to submit the contribution. Try again later.",
                statusCode: 502, title: "Bad Gateway");
        }
    }

    private ObjectResult? Validate(TranslationContributionRequest request)
    {
        if (!LocalePattern().IsMatch(request.Locale))
            return Problem(detail: $"Invalid locale: {request.Locale}", statusCode: 400, title: "Bad Request");

        if (request.Entries.Count is 0 or > MaxEntries)
            return Problem(detail: $"Between 1 and {MaxEntries} entries required", statusCode: 400, title: "Bad Request");

        foreach (var entry in request.Entries)
        {
            if (string.IsNullOrEmpty(entry.MsgId) || entry.MsgId.Length > MaxMsgIdLength)
                return Problem(detail: "Each entry needs a msgid under 4096 characters", statusCode: 400, title: "Bad Request");
            if (entry.Translations.Count is 0 or > MaxPluralForms
                || entry.Translations.Any(t => string.IsNullOrEmpty(t) || t.Length > MaxTranslationLength))
                return Problem(detail: "Each entry needs 1-8 non-empty translations under 8192 characters", statusCode: 400, title: "Bad Request");
        }

        if (string.IsNullOrWhiteSpace(request.Contributor.Name) || request.Contributor.Name.Length > 128)
            return Problem(detail: "Contributor name is required and must be under 128 characters", statusCode: 400, title: "Bad Request");

        if (request.Note?.Length > 2000)
            return Problem(detail: "Note must be under 2000 characters", statusCode: 400, title: "Bad Request");

        return null;
    }
}
