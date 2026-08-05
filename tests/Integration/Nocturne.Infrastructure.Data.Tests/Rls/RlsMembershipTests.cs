using FluentAssertions;
using Npgsql;
using Xunit;

namespace Nocturne.Infrastructure.Data.Tests.Rls;

/// <summary>
/// Behavioural assertions for the <c>tenant_isolation</c> policies on the three membership tables.
/// Raw Npgsql throughout, so what is covered is what PostgreSQL does — the EF query filters are a
/// separate gate, covered by the unit tests.
/// </summary>
/// <remarks>
/// <para>
/// The stakes are asymmetric. <c>tenant_members</c> is read on the authentication hot path of every
/// request, so a policy that denies too much is an instance-wide lockout; the enrolment anti-join
/// reads it across tenants, so a policy that hides too much silently becomes a cross-tenant passkey
/// takeover. Both directions are asserted.
/// </para>
/// <para>
/// Tenants and subjects are generated per test, so the shared fixture is safe to reuse — each test
/// asserts only against rows it inserted.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Collection("RLS completeness")]
public class RlsMembershipTests
{
    private readonly RlsCompletenessFixture _fx;

    public RlsMembershipTests(RlsCompletenessFixture fx) => _fx = fx;

    // ── The tenant arm: the authentication hot path ───────────────────────────

    /// <summary>
    /// The lockout pin. This is the <c>TenantMemberService.IsMemberAsync</c> shape, run on every
    /// authenticated request: if a tenant cannot see its own membership rows, nobody can log in.
    /// </summary>
    [Fact]
    public async Task PinnedToItsOwnTenant_TheMembershipIsVisible()
    {
        var t = await SeedMembershipAsync();

        await using var conn = await _fx.OpenAppConnectionAsync();
        await SetTenantAsync(conn, t.TenantId);

        (await CountMembersAsync(conn, t.SubjectId)).Should().Be(1,
            "a tenant must see its own membership, or every request to it fails authentication");
        (await CountMemberRolesAsync(conn, t.MemberId)).Should().Be(1,
            "the member's role assignment must be reachable under the same pin");
        (await CountRolesAsync(conn, t.RoleId)).Should().Be(1,
            "the role itself must be reachable under the same pin");
    }

    [Fact]
    public async Task WithNoTenantContext_AllThreeTablesAreEmpty()
    {
        var t = await SeedMembershipAsync();

        await using var conn = await _fx.OpenAppConnectionAsync();

        (await CountMembersAsync(conn, t.SubjectId)).Should().Be(0,
            "an unpinned connection compares tenant_id against NULL and matches nothing");
        (await CountMemberRolesAsync(conn, t.MemberId)).Should().Be(0,
            "tenant_member_roles inherits the denial through its EXISTS on tenant_members");
        (await CountRolesAsync(conn, t.RoleId)).Should().Be(0);
    }

    [Fact]
    public async Task PinnedToAnotherTenant_AllThreeTablesAreEmpty()
    {
        var t = await SeedMembershipAsync();
        var otherTenant = await SeedTenantAsync();

        await using var conn = await _fx.OpenAppConnectionAsync();
        await SetTenantAsync(conn, otherTenant);

        (await CountMembersAsync(conn, t.SubjectId)).Should().Be(0,
            "another tenant's membership must be invisible");
        (await CountMemberRolesAsync(conn, t.MemberId)).Should().Be(0);
        (await CountRolesAsync(conn, t.RoleId)).Should().Be(0);
    }

    [Fact]
    public async Task MigratorRole_WithoutTenantContext_ObeysForceRls()
    {
        var t = await SeedMembershipAsync();

        await using var conn = await _fx.OpenMigratorConnectionAsync();

        (await CountMembersAsync(conn, t.SubjectId)).Should().Be(0,
            "FORCE ROW LEVEL SECURITY must apply to the table owner too");
    }

    // ── The subject arm: reach over one person's own memberships ──────────────

    /// <summary>
    /// The tenant-switcher and caregiver-overview shape: the subject GUC alone, with no tenant
    /// pinned, reaches exactly that subject's memberships across every tenant and nobody else's.
    /// </summary>
    [Fact]
    public async Task WithOnlyTheSubjectGuc_ASubjectSeesTheirOwnMembershipsAcrossTenants()
    {
        var subjectId = await SeedSubjectAsync();
        var first = await SeedMembershipAsync(subjectId: subjectId);
        var second = await SeedMembershipAsync(subjectId: subjectId);
        var somebodyElse = await SeedMembershipAsync();

        await using var conn = await _fx.OpenAppConnectionAsync();
        await SetSubjectAsync(conn, subjectId);

        (await CountMembersAsync(conn, subjectId)).Should().Be(2,
            "the subject arm must reach this subject's memberships in every tenant");
        (await CountMembersAsync(conn, somebodyElse.SubjectId)).Should().Be(0,
            "the subject arm must not widen to anybody else's memberships");

        // The tenant-overview read walks memberships into their roles, so both links of the
        // inheritance chain have to carry the same reach.
        (await CountMemberRolesAsync(conn, first.MemberId)).Should().Be(1,
            "tenant_member_roles inherits the subject arm through its EXISTS on tenant_members");
        (await CountMemberRolesAsync(conn, second.MemberId)).Should().Be(1);
        (await CountRolesAsync(conn, first.RoleId)).Should().Be(1,
            "tenant_roles inherits it one step further, through tenant_member_roles");
        (await CountMemberRolesAsync(conn, somebodyElse.MemberId)).Should().Be(0);
        (await CountRolesAsync(conn, somebodyElse.RoleId)).Should().Be(0,
            "a role nobody reachable holds must stay invisible");
    }

    /// <summary>
    /// The passkey takeover guard. <c>PasskeyController.FindEnrollingSubjectIdAsync</c> refuses to
    /// enrol a credential onto a subject that holds membership anywhere, and probes that per
    /// candidate under the subject's own reach. Hide the row and the probe reads "no membership",
    /// which enrols the caller's authenticator onto somebody else's account — failing open, and
    /// silently. This asserts the row stays visible from a connection pinned to a different tenant.
    /// </summary>
    [Fact]
    public async Task PinnedToOneTenantWithAnotherSubjectsGuc_TheirOtherTenantMembershipStaysVisible()
    {
        var victim = await SeedSubjectAsync();
        var inTenantB = await SeedMembershipAsync(subjectId: victim);
        var tenantA = await SeedTenantAsync();

        await using var conn = await _fx.OpenAppConnectionAsync();
        await SetTenantAndSubjectAsync(conn, tenantA, victim);

        (await CountMembersAsync(conn, victim)).Should().Be(1,
            "the enrolment probe must see a membership held in another tenant, or it enrols onto it");
        inTenantB.TenantId.Should().NotBe(tenantA);
    }

    // ── Writes are tenant-pinned only ────────────────────────────────────────

    [Fact]
    public async Task InsertingAMembershipForAnotherTenant_IsRefused()
    {
        var sessionTenant = await SeedTenantAsync();
        var otherTenant = await SeedTenantAsync();
        var subjectId = await SeedSubjectAsync();

        await using var conn = await _fx.OpenAppConnectionAsync();
        await SetTenantAsync(conn, sessionTenant);

        var act = () => InsertMemberAsync(conn, otherTenant, subjectId);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501",
            "WITH CHECK is tenant-pinned, so a membership row for another tenant is refused");
    }

    /// <summary>
    /// The subject arm is read reach, never write reach: it appears in no WITH CHECK clause, so
    /// holding the subject GUC does not let a caller mint themselves a membership.
    /// </summary>
    [Fact]
    public async Task TheSubjectGuc_DoesNotAdmitAMembershipWrite()
    {
        var sessionTenant = await SeedTenantAsync();
        var otherTenant = await SeedTenantAsync();
        var subjectId = await SeedSubjectAsync();

        await using var conn = await _fx.OpenAppConnectionAsync();
        await SetTenantAndSubjectAsync(conn, sessionTenant, subjectId);

        var act = () => InsertMemberAsync(conn, otherTenant, subjectId);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501",
            "the subject arm must never become write reach");
    }

    [Fact]
    public async Task InsertingARoleForAnotherTenant_IsRefused()
    {
        var sessionTenant = await SeedTenantAsync();
        var otherTenant = await SeedTenantAsync();

        await using var conn = await _fx.OpenAppConnectionAsync();
        await SetTenantAsync(conn, sessionTenant);

        var act = () => InsertRoleAsync(conn, otherTenant);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501");
    }

    /// <summary>
    /// <c>tenant_member_roles</c> has no tenant of its own, so its WITH CHECK asks whether the
    /// parent membership belongs to the pinned tenant. A role assignment onto another tenant's
    /// membership is refused even though the row itself names no tenant.
    /// </summary>
    [Fact]
    public async Task AssigningARoleOntoAnotherTenantsMembership_IsRefused()
    {
        var elsewhere = await SeedMembershipAsync();
        var sessionTenant = await SeedTenantAsync();

        await using var conn = await _fx.OpenAppConnectionAsync();
        await SetTenantAsync(conn, sessionTenant);

        var act = () => InsertMemberRoleAsync(conn, elsewhere.MemberId, elsewhere.RoleId);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("42501",
            "the parent membership's tenant must match the pin");
    }

    [Fact]
    public async Task AssigningARoleOntoItsOwnTenantsMembership_IsAdmitted()
    {
        var t = await SeedMembershipAsync();
        var secondRole = await SeedRoleAsync(t.TenantId);

        await using var conn = await _fx.OpenAppConnectionAsync();
        await SetTenantAsync(conn, t.TenantId);

        await InsertMemberRoleAsync(conn, t.MemberId, secondRole);

        (await CountMemberRolesAsync(conn, t.MemberId)).Should().Be(2,
            "a role assignment whose parent membership is in the pinned tenant must be admitted");
    }

    // ── Public shares ────────────────────────────────────────────────────────

    /// <summary>
    /// Membership data is never share-visible. Neither table is classified in
    /// <c>ShareDataCategories</c>, so the startup reconciler's restrictive
    /// <c>share_category_read</c> policy denies both to a share — and denies
    /// <c>tenant_member_roles</c> with it, through the EXISTS, without a policy of its own.
    /// </summary>
    [Fact]
    public async Task AShare_SeesNoneOfTheMembershipTables()
    {
        var t = await SeedMembershipAsync();

        await using var conn = await _fx.OpenAppConnectionAsync();
        await SetShareAsync(conn, t.TenantId, isShare: true);

        (await CountMembersAsync(conn, t.SubjectId)).Should().Be(0,
            "tenant_members is unclassified, so the restrictive share policy denies it");
        (await CountRolesAsync(conn, t.RoleId)).Should().Be(0);
        (await CountMemberRolesAsync(conn, t.MemberId)).Should().Be(0,
            "tenant_member_roles has no share policy of its own — the denial is inherited");
    }

    /// <summary>
    /// The other side of the same coin, and the one that would take the share feature down:
    /// <c>PublicAccessCacheService</c> resolves the Public subject's membership on a tenant-pinned
    /// context that is deliberately NOT flagged as a share. That read must still work, or no
    /// anonymous share can resolve any permissions at all.
    /// </summary>
    [Fact]
    public async Task ANonShareContextOnTheSameTenant_StillReadsTheMembershipTables()
    {
        var t = await SeedMembershipAsync();

        await using var conn = await _fx.OpenAppConnectionAsync();
        await SetShareAsync(conn, t.TenantId, isShare: false);

        (await CountMembersAsync(conn, t.SubjectId)).Should().Be(1,
            "the share policy must not restrict a non-share connection");
        (await CountMemberRolesAsync(conn, t.MemberId)).Should().Be(1);
        (await CountRolesAsync(conn, t.RoleId)).Should().Be(1);
    }

    // ── Seeding ──────────────────────────────────────────────────────────────

    private sealed record SeededMembership(Guid TenantId, Guid SubjectId, Guid MemberId, Guid RoleId);

    /// <summary>
    /// Seeds a tenant with one role, one subject and a membership holding that role. The migrator
    /// obeys FORCE RLS too, so the tenant GUC is set before the tenant-scoped inserts.
    /// </summary>
    private async Task<SeededMembership> SeedMembershipAsync(Guid? subjectId = null)
    {
        var tenantId = Guid.NewGuid();
        var resolvedSubject = subjectId ?? Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        await using var conn = await _fx.OpenMigratorConnectionAsync();
        await InsertTenantAsync(conn, tenantId);
        if (subjectId is null)
        {
            await InsertSubjectAsync(conn, resolvedSubject);
        }

        await SetTenantAsync(conn, tenantId);
        await InsertRoleAsync(conn, tenantId, roleId);
        await InsertMemberAsync(conn, tenantId, resolvedSubject, memberId);
        await InsertMemberRoleAsync(conn, memberId, roleId);

        return new SeededMembership(tenantId, resolvedSubject, memberId, roleId);
    }

    private async Task<Guid> SeedTenantAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var conn = await _fx.OpenMigratorConnectionAsync();
        await InsertTenantAsync(conn, tenantId);
        return tenantId;
    }

    private async Task<Guid> SeedSubjectAsync()
    {
        var subjectId = Guid.NewGuid();
        await using var conn = await _fx.OpenMigratorConnectionAsync();
        await InsertSubjectAsync(conn, subjectId);
        return subjectId;
    }

    private async Task<Guid> SeedRoleAsync(Guid tenantId)
    {
        var roleId = Guid.NewGuid();
        await using var conn = await _fx.OpenMigratorConnectionAsync();
        await SetTenantAsync(conn, tenantId);
        await InsertRoleAsync(conn, tenantId, roleId);
        return roleId;
    }

    private static async Task InsertTenantAsync(NpgsqlConnection conn, Guid tenantId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tenants (id, slug, display_name, is_active, sys_created_at, sys_updated_at)
            VALUES (@id, @slug, 'rls-membership-test', true, now(), now())
            """;
        AddParam(cmd, "@id", tenantId);
        AddParam(cmd, "@slug", $"rlsmem-{tenantId:N}");
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertSubjectAsync(NpgsqlConnection conn, Guid subjectId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO subjects (id, name, approval_status, is_active, is_platform_admin, is_system_subject)
            VALUES (@id, 'rls-membership-subject', 'approved', true, false, false)
            """;
        AddParam(cmd, "@id", subjectId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertRoleAsync(NpgsqlConnection conn, Guid tenantId, Guid? roleId = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tenant_roles
                (id, tenant_id, name, slug, permissions, is_system, sys_created_at, sys_updated_at)
            VALUES (@id, @tid, 'Viewer', @slug, '["glucose.read"]'::jsonb, false, now(), now())
            """;
        var resolved = roleId ?? Guid.NewGuid();
        AddParam(cmd, "@id", resolved);
        AddParam(cmd, "@tid", tenantId);
        // slug is unique per tenant, so keep it distinct across the roles a test seeds.
        AddParam(cmd, "@slug", $"viewer-{resolved:N}");
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertMemberAsync(
        NpgsqlConnection conn, Guid tenantId, Guid subjectId, Guid? memberId = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tenant_members
                (id, tenant_id, subject_id, limit_to_24_hours, sys_created_at, sys_updated_at)
            VALUES (@id, @tid, @sid, false, now(), now())
            """;
        AddParam(cmd, "@id", memberId ?? Guid.NewGuid());
        AddParam(cmd, "@tid", tenantId);
        AddParam(cmd, "@sid", subjectId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertMemberRoleAsync(NpgsqlConnection conn, Guid memberId, Guid roleId)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO tenant_member_roles (id, tenant_member_id, tenant_role_id, sys_created_at)
            VALUES (gen_random_uuid(), @mid, @rid, now())
            """;
        AddParam(cmd, "@mid", memberId);
        AddParam(cmd, "@rid", roleId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Reads and session state ──────────────────────────────────────────────

    // Counted by primary/foreign key with no tenant predicate: the policy, not the query, is what
    // must do the scoping. A predicate here would pass whether or not the policy worked.
    private static Task<long> CountMembersAsync(NpgsqlConnection conn, Guid subjectId) =>
        CountAsync(conn, "SELECT COUNT(*) FROM tenant_members WHERE subject_id = @p", subjectId);

    private static Task<long> CountMemberRolesAsync(NpgsqlConnection conn, Guid memberId) =>
        CountAsync(conn, "SELECT COUNT(*) FROM tenant_member_roles WHERE tenant_member_id = @p", memberId);

    private static Task<long> CountRolesAsync(NpgsqlConnection conn, Guid roleId) =>
        CountAsync(conn, "SELECT COUNT(*) FROM tenant_roles WHERE id = @p", roleId);

    private static async Task<long> CountAsync(NpgsqlConnection conn, string sql, Guid parameter)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        AddParam(cmd, "@p", parameter);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static Task SetTenantAsync(NpgsqlConnection conn, Guid tenantId) =>
        SetSessionAsync(conn, tenantId.ToString(), string.Empty, isShare: null);

    private static Task SetSubjectAsync(NpgsqlConnection conn, Guid subjectId) =>
        SetSessionAsync(conn, string.Empty, subjectId.ToString(), isShare: null);

    private static Task SetTenantAndSubjectAsync(NpgsqlConnection conn, Guid tenantId, Guid subjectId) =>
        SetSessionAsync(conn, tenantId.ToString(), subjectId.ToString(), isShare: null);

    private static Task SetShareAsync(NpgsqlConnection conn, Guid tenantId, bool isShare) =>
        SetSessionAsync(conn, tenantId.ToString(), string.Empty, isShare);

    /// <summary>
    /// Sets the session GUCs the interceptor would set. An empty string stands for "unset": the
    /// policies read every GUC through <c>NULLIF(..., '')</c>, so empty and absent behave alike.
    /// </summary>
    private static async Task SetSessionAsync(
        NpgsqlConnection conn, string tenantId, string subjectId, bool? isShare)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT set_config('app.current_tenant_id', @tid, false), " +
            "set_config('app.current_subject_id', @sid, false), " +
            "set_config('app.is_share', @share, false)";
        AddParam(cmd, "@tid", tenantId);
        AddParam(cmd, "@sid", subjectId);
        AddParam(cmd, "@share", isShare switch { true => "true", false => "false", null => string.Empty });
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddParam(NpgsqlCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
