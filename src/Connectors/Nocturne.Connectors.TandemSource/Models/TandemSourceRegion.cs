namespace Nocturne.Connectors.TandemSource.Models;

public sealed class TandemSourceRegion
{
    public string LoginApiUrl { get; init; } = default!;
    public string AuthorizationEndpoint { get; init; } = default!;
    public string TokenEndpoint { get; init; } = default!;
    public string JwksUrl { get; init; } = default!;
    public string Issuer { get; init; } = default!;
    public string ClientId { get; init; } = default!;
    public string RedirectUri { get; init; } = default!;
    public string SourceBaseUrl { get; init; } = default!;
    public string SsoUrl { get; init; } = default!;

    public static TandemSourceRegion Us { get; } = new()
    {
        LoginApiUrl = "https://tdcservices.tandemdiabetes.com/accounts/api/login",
        AuthorizationEndpoint = "https://tdcservices.tandemdiabetes.com/accounts/api/connect/authorize",
        TokenEndpoint = "https://tdcservices.tandemdiabetes.com/accounts/api/connect/token",
        JwksUrl = "https://tdcservices.tandemdiabetes.com/accounts/api/.well-known/openid-configuration/jwks",
        Issuer = "https://tdcservices.tandemdiabetes.com/accounts/api",
        ClientId = "0oa27ho9tpZE9Arjy4h7",
        RedirectUri = "https://sso.tandemdiabetes.com/auth/callback",
        SourceBaseUrl = "https://source.tandemdiabetes.com/",
        SsoUrl = "https://sso.tandemdiabetes.com/"
    };

    public static TandemSourceRegion Eu { get; } = new()
    {
        LoginApiUrl = "https://tdcservices.eu.tandemdiabetes.com/accounts/api/login",
        AuthorizationEndpoint = "https://tdcservices.eu.tandemdiabetes.com/accounts/api/connect/authorize",
        TokenEndpoint = "https://tdcservices.eu.tandemdiabetes.com/accounts/api/connect/token",
        JwksUrl = "https://tdcservices.eu.tandemdiabetes.com/accounts/api/.well-known/openid-configuration/jwks",
        Issuer = "https://tdcservices.eu.tandemdiabetes.com/accounts/api",
        ClientId = "1519e414-eeec-492e-8c5e-97bea4815a10",
        RedirectUri = "https://source.eu.tandemdiabetes.com/authorize/callback",
        SourceBaseUrl = "https://source.eu.tandemdiabetes.com/",
        SsoUrl = "https://sso.tandemdiabetes.com/"
    };

    public static TandemSourceRegion ForServer(string server) =>
        server.Equals("EU", StringComparison.OrdinalIgnoreCase) ? Eu : Us;
}
