using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;

namespace Microsoft.Extensions.Hosting;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        // EF Core logs every executed SQL command at Information by default. Under
        // load (e.g. connector ingest) this floods stdout and the OTEL pipeline
        // with thousands of entries per second. Suppress at the source so all
        // downstream providers (Console, OTEL) see the reduced volume.
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Note: Standard resilience is NOT added globally here because connectors
            // configure their own resilience with longer timeouts suited for external APIs.
            // The default 10-second per-attempt timeout conflicts with connector HTTP calls
            // that may take longer. Services that need standard resilience (e.g., compatibility
            // proxy) add it explicitly via AddStandardResilienceHandler().

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Configure Kestrel to use dynamic ports from Aspire
        // This prevents port conflicts when running multiple connectors
        // If Aspire hasn't set ASPNETCORE_URLS, we'll use a random port
        if (string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]))
        {
            // Set ASPNETCORE_URLS to bind to a random port on localhost
            // Port 0 tells Kestrel to pick any available port
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:0");
        }

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static IHostApplicationBuilder ConfigureOpenTelemetry(
        this IHostApplicationBuilder builder
    )
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder
            .Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddNocturneResourceAttributes(builder))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(
        this IHostApplicationBuilder builder
    )
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
        );

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    /// <summary>
    /// Adds Nocturne-wide resource attributes (service.namespace, service.version,
    /// deployment.environment) so that every emitted log/metric/trace carries
    /// consistent identifying metadata. service.name is left to Aspire / OTEL_SERVICE_NAME.
    /// </summary>
    private static ResourceBuilder AddNocturneResourceAttributes(
        this ResourceBuilder resource,
        IHostApplicationBuilder builder
    )
    {
        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? "unknown";

        // Strip any "+gitsha" suffix
        var plusIndex = version.IndexOf('+');
        if (plusIndex > 0) version = version[..plusIndex];

        return resource.AddAttributes(new KeyValuePair<string, object>[]
        {
            new("service.namespace", "nocturne"),
            new("service.version", version),
            new("deployment.environment", builder.Environment.EnvironmentName.ToLowerInvariant()),
        });
    }

    public static IHostApplicationBuilder AddDefaultHealthChecks(
        this IHostApplicationBuilder builder
    )
    {
        builder
            .Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Map health check endpoints for all environments
        // These are required for:
        // 1. Aspire dashboard health monitoring
        // 2. Inter-service health checks (e.g., API checking connector status)
        // 3. Kubernetes/container orchestration liveness/readiness probes
        // Note: For production deployments exposed to the internet, consider
        // adding authentication or restricting access via network policies

        // All health checks must pass for app to be considered ready to accept traffic after starting
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteResponse
        });

        // Only health checks tagged with the "live" tag must pass for app to be considered alive
        app.MapHealthChecks(
            "/alive",
            new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") }
        );

        return app;
    }

    private static Task WriteResponse(Microsoft.AspNetCore.Http.HttpContext context, HealthReport result)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var options = new System.Text.Json.JsonWriterOptions
        {
            Indented = true
        };

        using var stream = new System.IO.MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream, options))
        {
            writer.WriteStartObject();
            writer.WriteString("status", result.Status.ToString());
            writer.WriteStartObject("results");
            foreach (var entry in result.Entries)
            {
                writer.WriteStartObject(entry.Key);
                writer.WriteString("status", entry.Value.Status.ToString());
                writer.WriteString("description", entry.Value.Description);
                writer.WriteStartObject("data");
                foreach (var item in entry.Value.Data)
                {
                    writer.WritePropertyName(item.Key);
                    System.Text.Json.JsonSerializer.Serialize(writer, item.Value, item.Value?.GetType() ?? typeof(object));
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        return context.Response.WriteAsync(json);
    }
}
