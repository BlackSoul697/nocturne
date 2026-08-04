using Nocturne.API.Multitenancy;
using Nocturne.Core.Constants;
using Nocturne.Core.Models.Configuration;

namespace Nocturne.API.Helpers;

/// <summary>
/// Resolves the deployment's public, externally reachable base URL — the origin an end
/// user's browser (or an identity provider redirecting one) actually reaches.
/// </summary>
/// <remarks>
/// <para>
/// Three sources, in order:
/// <list type="number">
///   <item><description>the flat <c>BaseUrl</c> key (<see cref="ServiceNames.ConfigKeys.BaseUrl"/>) — a full URL;</description></item>
///   <item><description><c>Oidc:BaseUrl</c> (<see cref="OidcOptions.BaseUrl"/>) — the same value under the OIDC section;</description></item>
///   <item><description><c>BASE_DOMAIN</c> (<see cref="BaseDomainOptions.ConfigKey"/>) — the bare
///   <c>host[:port]</c> every deployment already sets for subdomain tenant routing, promoted to
///   <c>https://host[:port]</c>.</description></item>
/// </list>
/// The <c>BASE_DOMAIN</c> fallback is what makes the Compose and Portainer bundles work
/// unconfigured: they set <c>BASE_DOMAIN</c> and nothing else, so without it every
/// external-facing URL the API builds (OIDC redirect URIs above all) collapsed to a
/// hard-coded <c>http://localhost:5000</c>.
/// </para>
/// <para>
/// This is deliberately NOT the address to use for deployment-internal, service-to-service
/// calls — see the hairpin note on <c>ChatBotProvider</c>. Those read <c>WEB_URL</c> /
/// <c>NocturneApiUrl</c> instead.
/// </para>
/// </remarks>
/// <seealso cref="BaseDomainOptions"/>
public static class PublicBaseUrl
{
    private const string OidcBaseUrlKey = $"{OidcOptions.SectionName}:BaseUrl";

    /// <summary>
    /// Resolves the public base URL, without a trailing slash.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>
    /// An absolute origin such as <c>https://example.com</c>, or <see langword="null"/> when the
    /// deployment configures neither a base URL nor a base domain.
    /// </returns>
    public static string? Resolve(IConfiguration configuration)
    {
        var configured = FirstNonEmpty(
            configuration[ServiceNames.ConfigKeys.BaseUrl],
            configuration[OidcBaseUrlKey],
            configuration[BaseDomainOptions.ConfigKey]
        );

        return configured is null ? null : Normalize(configured);
    }

    /// <summary>
    /// Determines whether an absolute URL points at this deployment — the base URL's own host,
    /// the base domain, or any tenant subdomain beneath either.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="candidate">The absolute URL to test.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="candidate"/> is an http(s) URL whose host is
    /// this deployment's; <see langword="false"/> otherwise, including when nothing is configured
    /// to compare against.
    /// </returns>
    /// <remarks>
    /// Host comparison, not string prefixing: <c>https://example.com.attacker.test/</c> shares a
    /// prefix with <c>https://example.com</c> but is not this deployment.
    /// </remarks>
    public static bool BelongsToDeployment(IConfiguration configuration, Uri candidate)
    {
        if (candidate.Scheme != Uri.UriSchemeHttp && candidate.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        foreach (var known in KnownHosts(configuration))
        {
            if (string.Equals(candidate.Host, known, StringComparison.OrdinalIgnoreCase)
                || candidate.Host.EndsWith($".{known}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The hosts (no port) this deployment answers on, from whichever of the three keys are set.
    /// </summary>
    private static IEnumerable<string> KnownHosts(IConfiguration configuration)
    {
        foreach (
            var value in new[]
            {
                configuration[ServiceNames.ConfigKeys.BaseUrl],
                configuration[OidcBaseUrlKey],
                configuration[BaseDomainOptions.ConfigKey],
            }
        )
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (Uri.TryCreate(Normalize(value), UriKind.Absolute, out var uri)
                && !string.IsNullOrEmpty(uri.Host))
            {
                yield return uri.Host;
            }
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>
    /// Trims a trailing slash and promotes a bare <c>host[:port]</c> (the shape
    /// <c>BASE_DOMAIN</c> carries) to an absolute HTTPS origin. A value that already names a
    /// scheme keeps it, so an operator who sets <c>BASE_DOMAIN=http://box.lan:8080</c> is not
    /// silently switched to HTTPS.
    /// </summary>
    private static string Normalize(string value)
    {
        var trimmed = value.Trim().TrimEnd('/');

        return trimmed.Contains("://", StringComparison.Ordinal)
            ? trimmed
            : $"https://{trimmed}";
    }
}
