using Microsoft.EntityFrameworkCore;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Services.Alerts;

/// <summary>
/// One-time startup backfill that stamps <c>scope_class</c> on every existing alert rule for
/// scoped Do Not Disturb (ADR 0004). Pre-existing rules were created before classification
/// existed and default to <c>undirected</c> (all-only) from the D2 migration; this recomputes
/// each one through <see cref="IRuleScopeClassifier"/> so scoped <c>lows</c>/<c>highs</c> windows
/// can narrow-match them. After this lands, the controller computes <c>scope_class</c> on every
/// create/update, so the steady state is a no-op.
/// </summary>
/// <remarks>
/// Idempotent and safe to run on every startup: only rows whose recomputed class differs from the
/// stored one are written, so once the population is classified the scan writes nothing. The scan
/// is per-tenant because <c>alert_rules</c> is RLS-scoped and the policy is fail-closed — a
/// cross-tenant <c>IgnoreQueryFilters</c> read would be blocked, so we set the tenant context per
/// iteration exactly like the sweep service does.
///
/// A <see cref="BackgroundService"/> rather than a bare <see cref="IHostedService"/>: the work is
/// a full scan of every rule of every tenant, and <c>StartAsync</c> runs before the host begins
/// serving, so doing it there delays readiness for as long as the scan takes. Nothing depends on
/// the backfill having completed — unclassified rules read as <c>undirected</c>, which is the
/// all-only fallback the gate already handles.
/// </remarks>
/// <seealso cref="RuleScopeClassifier"/>
public sealed class RuleScopeClassBackfillService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RuleScopeClassBackfillService> _logger;

    public RuleScopeClassBackfillService(
        IServiceProvider serviceProvider,
        ILogger<RuleScopeClassBackfillService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // ExecuteAsync runs synchronously up to its first await, and the host waits on that
        // stretch — yield before touching DI or the native probe so none of the scan is on the
        // startup path.
        await Task.Yield();

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
            var classifier = scope.ServiceProvider.GetRequiredService<IRuleScopeClassifier>();

            // Without the native engine every Classify returns the Undirected fallback, so the
            // scan can only ever write the value the D2 migration already defaulted to — while
            // logging one warning per rule per boot. Skip it and say so once.
            if (!classifier.IsAvailable)
            {
                _logger.LogWarning(
                    "Skipping alert-rule scope-class backfill: the nocturne_alerts native library "
                    + "could not be loaded, so every rule would classify as undirected (all-only) "
                    + "and scoped lows/highs Do Not Disturb cannot narrow-match any rule.");
                return;
            }

            // Tenants are not RLS-scoped, so this list read is safe without a tenant context.
            List<Guid> tenantIds;
            await using (var lookup = await factory.CreateDbContextAsync(cancellationToken))
            {
                tenantIds = await lookup.Tenants
                    .AsNoTracking()
                    .Where(t => t.IsActive)
                    .Select(t => t.Id)
                    .ToListAsync(cancellationToken);
            }

            var updatedTotal = 0;
            foreach (var tenantId in tenantIds)
            {
                await using var db = await factory.CreateDbContextAsync(cancellationToken);
                db.TenantId = tenantId;

                // Tracked (not AsNoTracking) so reclassified rows are persisted by SaveChanges.
                var rules = await db.AlertRules
                    .Where(r => r.TenantId == tenantId)
                    .ToListAsync(cancellationToken);

                var updated = 0;
                foreach (var rule in rules)
                {
                    var computed = classifier.Classify(rule.ConditionType, rule.ConditionParams);
                    if (rule.ScopeClass != computed)
                    {
                        rule.ScopeClass = computed;
                        updated++;
                    }
                }

                if (updated > 0)
                {
                    await db.SaveChangesAsync(cancellationToken);
                    updatedTotal += updated;
                }
            }

            if (updatedTotal > 0)
            {
                _logger.LogInformation(
                    "Backfilled scope_class for {Count} alert rule(s) across {Tenants} tenant(s)",
                    updatedTotal,
                    tenantIds.Count);
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutting down mid-scan; the next boot picks up where this left off.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error backfilling alert-rule scope classes");
        }
    }
}
