namespace Nocturne.Infrastructure.Data.Security;

/// <summary>
/// Tables under Row Level Security that carry no <c>tenant_id</c> of their own, and so are
/// invisible to the <see cref="Entities.ITenantScoped"/> model walk that drives the startup
/// self-check. Their policies derive the tenant from a parent row instead.
/// </summary>
/// <remarks>
/// The startup verification and the RLS completeness test both union this set with the
/// tenant-scoped tables, so a policy silently dropped from one of these fails the boot exactly
/// as it would on a tenant-scoped table. Adding a <c>tenant_id</c> column to one of these tables
/// is not the fix — the point of the derived shape is that visibility inherits from the parent,
/// leaving membership the single source of truth.
/// </remarks>
public static class RlsProtectedTables
{
    /// <summary>
    /// Join tables whose RLS policy resolves the tenant through a parent row. <c>tenant_member_roles</c>
    /// tests membership through <c>tenant_members</c>, inheriting both arms of that table's policy
    /// (tenant pin and subject reach) as well as its restrictive share denial.
    /// </summary>
    public static readonly IReadOnlyList<string> DerivedTenantTables = ["tenant_member_roles"];
}
