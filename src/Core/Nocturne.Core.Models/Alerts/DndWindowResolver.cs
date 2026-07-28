namespace Nocturne.Core.Models.Alerts;

/// <summary>
/// The Do Not Disturb picture at one instant: which scopes are in force, and the
/// tenant-wide projection that drives the <c>do_not_disturb</c> condition leaf.
/// </summary>
/// <param name="Scopes">
/// The active scopes, for <see cref="DndSuppressionGate"/>. Empty when no DND is active.
/// </param>
/// <param name="ActiveDoNotDisturb">
/// Non-null exactly when <paramref name="Scopes"/> contains <see cref="DndScope.All"/> —
/// <c>lows</c>/<c>highs</c> windows are gate-only and never trip the condition leaf.
/// </param>
public readonly record struct DndResolution(
    IReadOnlySet<DndScope> Scopes,
    DoNotDisturbSnapshot? ActiveDoNotDisturb);

/// <summary>
/// Resolves a tenant's DND windows (plus scheduled DND) into the <see cref="DndResolution"/>
/// the evaluation context carries. The single resolver for both paths: the live enricher and
/// the replay walker call this instead of each assembling the scope set and the
/// <see cref="DoNotDisturbSnapshot"/> themselves, so they cannot disagree about whether a
/// tick was in DND (ADR 0004 D5).
/// </summary>
/// <seealso cref="DndWindowSnapshot"/>
/// <seealso cref="DndSuppressionGate"/>
public static class DndWindowResolver
{
    /// <summary>Shared empty set so the common no-DND instant allocates nothing.</summary>
    private static readonly IReadOnlySet<DndScope> NoScopes = new HashSet<DndScope>();

    /// <summary>
    /// The DND state in force at <paramref name="atUtc"/>.
    /// </summary>
    /// <param name="windows">The tenant's candidate windows (uncleared, or — for replay — receipt-bounded).</param>
    /// <param name="atUtc">The evaluation instant.</param>
    /// <param name="receiptGated">
    /// <see langword="true"/> for replay: a window only counts once the server had received it
    /// (<see cref="DndWindowSnapshot.WasActiveAt"/>), so replay never retroactively suppresses
    /// the offline-authoring gap. <see langword="false"/> for the live path.
    /// </param>
    /// <param name="scheduled">
    /// The active scheduled-DND projection, when any. Scheduled DND is tenant-wide, so it
    /// contributes <see cref="DndScope.All"/> and takes precedence as the
    /// <c>for_minutes</c> anchor over a manual all-window.
    /// </param>
    public static DndResolution Resolve(
        IEnumerable<DndWindowSnapshot> windows,
        DateTime atUtc,
        bool receiptGated,
        TenantAlertSettingsSnapshot.ActiveProjection? scheduled = null)
    {
        HashSet<DndScope>? scopes = null;
        DateTime? earliestAllStartedAt = null;

        foreach (var window in windows)
        {
            var active = receiptGated ? window.WasActiveAt(atUtc) : window.IsActiveAt(atUtc);
            if (!active)
                continue;

            (scopes ??= new HashSet<DndScope>()).Add(window.Scope);

            if (window.Scope == DndScope.All
                && (earliestAllStartedAt is null || window.StartedAt < earliestAllStartedAt))
            {
                earliestAllStartedAt = window.StartedAt;
            }
        }

        if (scheduled is not null)
            (scopes ??= new HashSet<DndScope>()).Add(DndScope.All);

        var resolvedScopes = scopes is null ? NoScopes : scopes;

        // Anchor for_minutes on the scheduled projection when present, else on the earliest
        // active all-window (a manual mute). The atUtc fallback is unreachable — All is only
        // in the set because one of those two produced it — but keeps the snapshot's
        // "StartedAt is always meaningful" contract true by construction.
        DoNotDisturbSnapshot? dnd = null;
        if (resolvedScopes.Contains(DndScope.All))
        {
            dnd = scheduled is not null
                ? new DoNotDisturbSnapshot(scheduled.StartedAt, scheduled.Source)
                : new DoNotDisturbSnapshot(earliestAllStartedAt ?? atUtc, "manual");
        }

        return new DndResolution(resolvedScopes, dnd);
    }
}
