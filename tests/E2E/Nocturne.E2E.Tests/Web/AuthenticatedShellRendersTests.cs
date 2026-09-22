using System.Net;
using FluentAssertions;
using Nocturne.E2E.Tests.Fixtures;
using Xunit;

namespace Nocturne.E2E.Tests.Web;

/// <summary>
/// Renders the signed-in shell through the real web server.
/// </summary>
/// <remarks>
/// Every other suite exercises the app below the rendering layer: the unit tests
/// call components in isolation, the API tests never render a page, and
/// <c>pnpm build</c> compiles the app without rendering one. A build that
/// succeeds can still throw on the first server render — the wuchale rollout did
/// exactly that, and the only page anyone had rendered by hand was the login
/// page, which sits outside the authenticated layout and so carries no sidebar.
/// This asks the running server for a page behind the session and refuses a body
/// that is SvelteKit's error shell instead of the app.
/// </remarks>
[Collection("e2e")]
[Trait("Category", "E2E")]
public class AuthenticatedShellRendersTests
{
    private readonly AppHostFixture _fixture;
    private readonly DevSeedClient _seed;

    public AuthenticatedShellRendersTests(AppHostFixture fixture)
    {
        _fixture = fixture;
        _seed = new DevSeedClient(fixture);
    }

    [Fact]
    public async Task Dashboard_RendersTheShell_ForASignedInSession()
    {
        var ctx = await _seed.SeedTenantAsync();

        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = false,
        };
        using var browser = new HttpClient(handler)
        {
            BaseAddress = new Uri(_fixture.GatewayBaseUrl),
        };
        browser.DefaultRequestHeaders.Host = $"{ctx.Slug}.{_fixture.GatewayHost}";

        // The dev login GET is the browser-session entry point: it leaves the
        // session cookies on the tenant host the web app is served from.
        var login = await browser.GetAsync(
            $"/api/v4/dev-only/auth/login?tenant={ctx.Slug}&username={ctx.Username}");
        ((int)login.StatusCode).Should().BeInRange(200, 399,
            "the dev login should issue a session rather than refuse it");

        var page = await browser.GetAsync("/");
        var body = await page.Content.ReadAsStringAsync();

        page.StatusCode.Should().Be(HttpStatusCode.OK,
            "a signed-in dashboard request must render, not fall through to the error shell");

        // The shell, not the error page. Both assertions earn their place: a
        // server-side render failure answers 500 with a body that still parses
        // as HTML, so only the sidebar marker proves the app rendered.
        body.Should().NotContain("An error occurred while processing your request",
            "that body is SvelteKit's error shell, which means the render threw");
        body.Should().Contain("data-slot=\"sidebar-wrapper\"",
            "the authenticated layout renders the sidebar provider's wrapper");
    }
}
