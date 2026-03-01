using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.TandemSource.Configurations;
using Nocturne.Connectors.TandemSource.Models;
using Nocturne.Connectors.TandemSource.Services;

namespace Nocturne.Connectors.TandemSource;

public static class ServiceCollectionExtensions
{
    public static void AddTandemSourceConnector(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var config = services.AddConnectorConfiguration<TandemSourceConnectorConfiguration>(
            configuration,
            "TandemSource"
        );
        if (!config.Enabled)
            return;

        var region = TandemSourceRegion.ForServer(config.Server);

        var cookieHandler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        };

        services.AddHttpClient<TandemSourceConnectorService>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        });

        services.AddHttpClient<TandemSourceAuthTokenProvider>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        });

        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = factory.CreateClient(nameof(TandemSourceAuthTokenProvider));
            var configOptions = sp.GetRequiredService<IOptions<TandemSourceConnectorConfiguration>>();
            var logger = sp.GetRequiredService<ILogger<TandemSourceAuthTokenProvider>>();
            return new TandemSourceAuthTokenProvider(configOptions, httpClient, logger);
        });
    }
}
