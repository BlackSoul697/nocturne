using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;

namespace Nocturne.API.Middleware;

/// <summary>
/// Pushes an ILogger scope containing the resolved tenant/subject/request
/// identifiers so that all downstream log entries (and OTel log records,
/// because IncludeScopes is enabled in ServiceDefaults) are stamped with
/// nocturne.tenant_id, nocturne.subject_id, http.route, and http.method.
///
/// TraceId / SpanId are added automatically by the OTel logging provider
/// when an Activity is in scope; we do not add them ourselves.
///
/// Placed AFTER AuthenticationMiddleware so AuthContext is populated, but
/// BEFORE controllers run so handler log lines pick up the scope.
/// </summary>
public class LogContextEnrichmentMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LogContextEnrichmentMiddleware> _logger;

    public LogContextEnrichmentMiddleware(
        RequestDelegate next,
        ILogger<LogContextEnrichmentMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = (context.Items["TenantContext"] as TenantContext)?.TenantId;
        var auth = context.Items["AuthContext"] as AuthContext;

        var state = new Dictionary<string, object?>
        {
            ["nocturne.tenant_id"] = tenantId?.ToString(),
            ["nocturne.subject_id"] = auth?.SubjectId?.ToString(),
            ["nocturne.auth_type"] = auth?.AuthType.ToString(),
            ["http.route"] = context.Request.Path.Value,
            ["http.method"] = context.Request.Method,
        };

        using (_logger.BeginScope(state))
        {
            await _next(context);
        }
    }
}
