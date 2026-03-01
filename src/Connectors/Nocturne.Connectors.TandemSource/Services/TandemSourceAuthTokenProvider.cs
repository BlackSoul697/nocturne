using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.TandemSource.Configurations;
using Nocturne.Connectors.TandemSource.Models;

namespace Nocturne.Connectors.TandemSource.Services;

public class TandemSourceAuthTokenProvider : AuthTokenProviderBase<TandemSourceConnectorConfiguration>
{
    private string? _pumperId;
    private string? _accountId;

    public TandemSourceAuthTokenProvider(
        IOptions<TandemSourceConnectorConfiguration> config,
        HttpClient httpClient,
        ILogger<TandemSourceAuthTokenProvider> logger
    ) : base(config.Value, httpClient, logger)
    {
    }

    public string? PumperId => _pumperId;
    public string? AccountId => _accountId;

    protected override int TokenLifetimeBufferMinutes => 5;

    protected override async Task<(string? Token, DateTime ExpiresAt)> AcquireTokenAsync(
        CancellationToken cancellationToken)
    {
        var region = TandemSourceRegion.ForServer(_config.Server);

        _logger.LogInformation("Authenticating with Tandem Source ({Region})...", _config.Server);

        // Step 1: POST login credentials
        var loginPayload = JsonSerializer.Serialize(new { username = _config.Email, password = _config.Password });
        var loginContent = new StringContent(loginPayload, Encoding.UTF8, "application/json");
        loginContent.Headers.Add("Referer", region.SsoUrl);

        // Load SSO page first to establish session cookies
        await _httpClient.GetAsync(region.SsoUrl, cancellationToken);

        var loginResponse = await _httpClient.PostAsync(region.LoginApiUrl, loginContent, cancellationToken);
        if (!loginResponse.IsSuccessStatusCode)
        {
            var errorBody = await loginResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Tandem Source login failed: {StatusCode} {Error}", loginResponse.StatusCode, errorBody);
            return (null, DateTime.MinValue);
        }

        var loginJson = await JsonSerializer.DeserializeAsync<JsonElement>(
            await loginResponse.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        if (loginJson.GetProperty("status").GetString() != "SUCCESS")
        {
            _logger.LogError("Tandem Source login returned non-SUCCESS status");
            return (null, DateTime.MinValue);
        }

        _logger.LogDebug("Tandem Source login successful, starting OIDC/PKCE flow");

        // Step 2: Generate PKCE code verifier and challenge
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Step 3: Authorization request (follow redirects to get code)
        var authParams = new Dictionary<string, string>
        {
            ["client_id"] = region.ClientId,
            ["response_type"] = "code",
            ["scope"] = "openid profile email",
            ["redirect_uri"] = region.RedirectUri,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var authUrl = $"{region.AuthorizationEndpoint}?{string.Join("&", authParams.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"))}";

        var authRequest = new HttpRequestMessage(HttpMethod.Get, authUrl);
        authRequest.Headers.Add("Referer", region.SsoUrl);

        var authResponse = await _httpClient.SendAsync(authRequest, cancellationToken);
        if (!authResponse.IsSuccessStatusCode)
        {
            var errorBody = await authResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Tandem Source authorization failed: {StatusCode} {Error}", authResponse.StatusCode, errorBody);
            return (null, DateTime.MinValue);
        }

        var finalUrl = authResponse.RequestMessage?.RequestUri?.ToString() ?? "";
        var queryParams = System.Web.HttpUtility.ParseQueryString(new Uri(finalUrl).Query);
        var authorizationCode = queryParams["code"];

        if (string.IsNullOrEmpty(authorizationCode))
        {
            _logger.LogError("No authorization code in callback URL: {Url}", finalUrl);
            return (null, DateTime.MinValue);
        }

        _logger.LogDebug("Got authorization code, exchanging for token");

        // Step 4: Token exchange
        var tokenParams = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = region.ClientId,
            ["code"] = authorizationCode,
            ["redirect_uri"] = region.RedirectUri,
            ["code_verifier"] = codeVerifier
        });

        var tokenResponse = await _httpClient.PostAsync(region.TokenEndpoint, tokenParams, cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Tandem Source token exchange failed: {StatusCode} {Error}", tokenResponse.StatusCode, errorBody);
            return (null, DateTime.MinValue);
        }

        var tokenJson = await JsonSerializer.DeserializeAsync<JsonElement>(
            await tokenResponse.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        if (!tokenJson.TryGetProperty("access_token", out var accessTokenElement) ||
            !tokenJson.TryGetProperty("id_token", out var idTokenElement))
        {
            _logger.LogError("Missing access_token or id_token in token response");
            return (null, DateTime.MinValue);
        }

        var accessToken = accessTokenElement.GetString()!;
        var idToken = idTokenElement.GetString()!;
        var expiresIn = tokenJson.TryGetProperty("expires_in", out var expiresElement)
            ? expiresElement.GetInt32()
            : 3600;

        // Step 5: Decode JWT to extract pumperId and accountId
        await ExtractJwtClaimsAsync(idToken, region, cancellationToken);

        var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
        _logger.LogInformation(
            "Tandem Source authentication successful (PumperId={PumperId}, expires at {ExpiresAt})",
            _pumperId, expiresAt);

        return (accessToken, expiresAt);
    }

    private async Task ExtractJwtClaimsAsync(string idToken, TandemSourceRegion region, CancellationToken cancellationToken)
    {
        try
        {
            var jwksResponse = await _httpClient.GetAsync(region.JwksUrl, cancellationToken);
            var jwksJson = await jwksResponse.Content.ReadAsStringAsync(cancellationToken);
            var jwks = new JsonWebKeySet(jwksJson);

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = region.Issuer,
                ValidateAudience = true,
                ValidAudience = region.ClientId,
                ValidateLifetime = true,
                IssuerSigningKeys = jwks.GetSigningKeys(),
                ValidateIssuerSigningKey = true
            };

            var principal = tokenHandler.ValidateToken(idToken, validationParams, out _);
            _pumperId = principal.FindFirst("pumperId")?.Value;
            _accountId = principal.FindFirst("accountId")?.Value;

            if (string.IsNullOrEmpty(_pumperId))
            {
                _logger.LogWarning("pumperId not found in JWT claims");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate JWT with JWKS, falling back to unverified decode");

            var tokenHandler = new JwtSecurityTokenHandler();
            var jwt = tokenHandler.ReadJwtToken(idToken);
            _pumperId = jwt.Claims.FirstOrDefault(c => c.Type == "pumperId")?.Value;
            _accountId = jwt.Claims.FirstOrDefault(c => c.Type == "accountId")?.Value;
        }
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
