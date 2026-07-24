using System.Security.Cryptography;
using System.Text;
using Nocturne.Core.Models.Authorization;

namespace Nocturne.API.Middleware.Handlers;

/// <summary>
/// Authentication handler for subject access tokens.
/// Two shapes are accepted, because tokens reach us from two places:
/// - Nocturne-minted: 64 lowercase hex chars, no dash (see <c>SubjectService.GenerateAccessToken</c>)
/// - Imported from classic Nightscout: {name_abbrev}-{hex_digest}, e.g. rhys-a1b2c3d4e5f6a7b8
/// Tokens can be provided via:
/// - Authorization header (Bearer token)
/// - Query parameter: ?token=xxx
/// - Request body: { "token": "xxx" }
/// </summary>
public class AccessTokenHandler : IAuthHandler
{
    /// <summary>
    /// Length of a Nocturne-minted access token in hex characters (32 random bytes).
    /// Kept in sync with <c>SubjectService.GenerateAccessToken</c>; the round-trip test in
    /// <c>AccessTokenHandlerTests</c> fails if the two ever drift apart.
    /// </summary>
    private const int AccessTokenHexLength = 64;

    /// <summary>
    /// Handler priority (300 - after JWT handlers, before API secret)
    /// </summary>
    public int Priority => 300;

    /// <summary>
    /// Handler name for logging
    /// </summary>
    public string Name => "AccessTokenHandler";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccessTokenHandler> _logger;

    /// <summary>
    /// Creates a new instance of AccessTokenHandler
    /// </summary>
    public AccessTokenHandler(IServiceScopeFactory scopeFactory, ILogger<AccessTokenHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AuthResult> AuthenticateAsync(HttpContext context)
    {
        // Try to extract access token from various locations
        var accessToken = ExtractAccessToken(context);

        if (string.IsNullOrEmpty(accessToken))
        {
            return AuthResult.Skip();
        }

        // Access tokens contain a dash separator: name-hash
        // JWTs have dots (.), so if we find dots it's not an access token
        if (accessToken.Contains('.'))
        {
            return AuthResult.Skip();
        }

        // Validate format: should have a dash and alphanumeric chars
        if (!IsValidAccessTokenFormat(accessToken))
        {
            _logger.LogDebug("Token doesn't match access token format, skipping");
            return AuthResult.Skip();
        }

        // Hash the token to look it up
        var tokenHash = ComputeSha256Hash(accessToken);

        // Look up the subject by access token hash
        using var scope = _scopeFactory.CreateScope();
        var subjectService = scope.ServiceProvider.GetRequiredService<ISubjectService>();

        try
        {
            var subject = await subjectService.GetSubjectByAccessTokenHashAsync(tokenHash);

            if (subject == null)
            {
                // Skip rather than fail: a non-skip result stops the whole handler chain
                // (see AuthenticationMiddleware), so failing here would block ApiKeyHandler
                // for a request that also carries a valid api-secret. Matches how
                // DirectGrantTokenHandler treats an unrecognized token.
                _logger.LogDebug("Access token not found in database");
                return AuthResult.Skip();
            }

            if (!subject.IsActive)
            {
                _logger.LogDebug("Subject {SubjectId} is deactivated", subject.Id);
                return AuthResult.Failure("Subject is deactivated");
            }

            // Get permissions for the subject
            var permissions = await subjectService.GetSubjectPermissionsAsync(subject.Id);
            var roles = await subjectService.GetSubjectRolesAsync(subject.Id);

            // Update last login timestamp (fire and forget)
            _ = subjectService.UpdateLastLoginAsync(subject.Id);

            // Create auth context
            var authContext = new AuthContext
            {
                IsAuthenticated = true,
                AuthType = AuthType.LegacyAccessToken,
                SubjectId = subject.Id,
                SubjectName = subject.Name,
                Email = subject.Email,
                Permissions = permissions,
                Roles = roles,
                RawToken = accessToken,
            };

            _logger.LogDebug(
                "Access token authentication successful for subject {SubjectName} ({SubjectId})",
                subject.Name,
                subject.Id
            );

            return AuthResult.Success(authContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating access token");
            return AuthResult.Failure("Token validation error");
        }
    }

    /// <summary>
    /// Extract access token from various request locations
    /// Priority: Header > Query > Body
    /// </summary>
    private static string? ExtractAccessToken(HttpContext context)
    {
        // 1. Check Authorization header (Bearer token that's not a JWT)
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (
            !string.IsNullOrEmpty(authHeader)
            && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        )
        {
            var token = authHeader["Bearer ".Length..].Trim();
            // Only return if it's NOT a JWT (no dots)
            if (!token.Contains('.'))
            {
                return token;
            }
        }

        // 2. Check query parameters
        var queryToken = context.Request.Query["token"].FirstOrDefault();
        if (!string.IsNullOrEmpty(queryToken))
        {
            return queryToken;
        }

        // 3. Check request body for JSON (only for POST/PUT/PATCH)
        // Note: Reading body is expensive and can only be done once unless buffering is enabled
        // We skip body parsing here and expect middleware to have buffered if needed

        return null;
    }

    /// <summary>
    /// Validate that the token matches one of the access token formats.
    /// This is only a cheap pre-filter to avoid a database lookup on values that clearly
    /// aren't access tokens — it must keep skipping JWTs (handled by the JWT handlers) and
    /// <c>noc_</c> direct grant tokens (handled by <see cref="DirectGrantTokenHandler"/>).
    /// </summary>
    private static bool IsValidAccessTokenFormat(string token)
    {
        // Nocturne-minted tokens: 32 random bytes, hex-encoded, no dash. Pinning the length
        // to exactly 64 keeps the filter tight: noc_ tokens carry a non-hex underscore, JWTs
        // are rejected on the dot check above, and pre-hashed legacy api-secrets (40 hex
        // chars, and sent via api-secret/?secret= anyway) are the wrong length.
        if (token.Length == AccessTokenHexLength && token.All(char.IsAsciiHexDigit))
        {
            return true;
        }

        // Imported Nightscout tokens: {name_abbrev}-{hex_digest}, digest typically 16 chars.
        var dashIndex = token.LastIndexOf('-');
        if (dashIndex <= 0 || dashIndex >= token.Length - 1)
        {
            return false;
        }

        // Characters after the dash should be alphanumeric (hex digest)
        var digest = token[(dashIndex + 1)..];
        if (digest.Length < 8) // Minimum reasonable digest length
        {
            return false;
        }

        // Allow alphanumeric characters in digest
        return digest.All(c => char.IsLetterOrDigit(c));
    }

    /// <summary>
    /// Compute SHA-256 hash of the access token for storage lookup
    /// </summary>
    private static string ComputeSha256Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
