using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRlsToMembershipTables : Migration
    {
        /// <summary>
        /// Enrols the three membership tables in the RLS regime. <c>tenant_members</c> and
        /// <c>tenant_roles</c> now implement <c>ITenantScoped</c>; <c>tenant_member_roles</c> does
        /// not and gains no column, because it has no tenant of its own — it is reachable only
        /// through the membership it belongs to.
        ///
        /// No schema change accompanies this: both tenant_id columns, their indexes and their
        /// cascading foreign keys already exist.
        ///
        /// <para>
        /// Each table gets two permissive policies rather than one. <c>tenant_isolation</c> is FOR
        /// ALL and purely tenant-pinned; <c>subject_read</c> is FOR SELECT and adds the subject's
        /// reach over their own rows. PostgreSQL ORs permissive policies per command, so SELECT sees
        /// both while INSERT, UPDATE and DELETE see only the tenant-pinned one. Folding the subject
        /// arm into a single FOR ALL policy's USING clause would have granted it DELETE as well —
        /// USING is what DELETE is checked against, and there is no WITH CHECK to stop it — letting
        /// a subject-pinned connection delete its own memberships in any tenant.
        /// </para>
        /// <para>
        /// The subject reach exists for the genuinely cross-tenant reads: the tenant switcher, the
        /// caregiver overview, and the passkey enrolment probe, whose anti-join must see a
        /// membership held in another tenant or it would enrol a credential onto somebody else's
        /// account. <c>tenant_members</c> is also read on the authentication hot path of every
        /// request, so its tenant arm has to stay wide enough that a tenant always sees its own
        /// members.
        /// </para>
        /// <para>
        /// <c>tenant_member_roles</c> inherits its visibility rather than restating it. PostgreSQL
        /// evaluates a policy expression as the querying role and applies RLS to the tables that
        /// expression references, so the bare EXISTS on <c>tenant_members</c> in its
        /// <c>subject_read</c> is itself filtered by both of that table's policies. That makes
        /// membership the single source of truth: widening or narrowing it propagates, and the
        /// restrictive <c>share_category_read</c> policy the startup reconciler puts on
        /// tenant_members (unclassified, so shares are denied) denies tenant_member_roles too
        /// without it needing a policy of its own.
        /// </para>
        /// <para>
        /// <c>tenant_member_roles</c> has no tenant column, so both sides of a row it creates are
        /// checked against the pinned tenant: the parent membership and the role being granted.
        /// Without the role-side conjunct a caller could link one of its own memberships to another
        /// tenant's role and inherit that role's permissions.
        /// </para>
        /// <para>
        /// That role-side conjunct is why <c>tenant_roles</c>' subject reach goes to
        /// <c>tenant_members</c> directly — "a role of a tenant this subject belongs to" — rather
        /// than chaining through <c>tenant_member_roles</c>. The two would otherwise reference each
        /// other and PostgreSQL refuses the cycle outright: inserting a role assignment would
        /// expand tenant_member_roles' WITH CHECK into tenant_roles' policies, which would re-enter
        /// tenant_member_roles, and the statement fails with 42P17 infinite recursion. Reaching
        /// through tenant_members keeps every chain acyclic (tenant_members references nothing) and
        /// still hides the roles of tenants the subject is not a member of, which is what the
        /// caregiver overview and tenant switcher need.
        /// </para>
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NULLIF + missing_ok keeps every policy expression safe to evaluate when the GUC is
            // unset, matching every other tenant-scoped table: an unpinned connection compares
            // against NULL and matches no row.
            migrationBuilder.Sql("ALTER TABLE tenant_members ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE tenant_members FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS tenant_isolation ON tenant_members;
                CREATE POLICY tenant_isolation ON tenant_members
                    FOR ALL
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

                DROP POLICY IF EXISTS subject_read ON tenant_members;
                CREATE POLICY subject_read ON tenant_members
                    FOR SELECT
                    USING (subject_id = NULLIF(current_setting('app.current_subject_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("ALTER TABLE tenant_member_roles ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE tenant_member_roles FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS tenant_isolation ON tenant_member_roles;
                CREATE POLICY tenant_isolation ON tenant_member_roles
                    FOR ALL
                    USING (EXISTS (
                        SELECT 1 FROM tenant_members tm
                        WHERE tm.id = tenant_member_roles.tenant_member_id
                          AND tm.tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid))
                    WITH CHECK (EXISTS (
                            SELECT 1 FROM tenant_members tm
                            WHERE tm.id = tenant_member_roles.tenant_member_id
                              AND tm.tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                        AND EXISTS (
                            SELECT 1 FROM tenant_roles tr
                            WHERE tr.id = tenant_member_roles.tenant_role_id
                              AND tr.tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid));

                DROP POLICY IF EXISTS subject_read ON tenant_member_roles;
                CREATE POLICY subject_read ON tenant_member_roles
                    FOR SELECT
                    USING (EXISTS (
                        SELECT 1 FROM tenant_members tm
                        WHERE tm.id = tenant_member_roles.tenant_member_id));
                """);

            migrationBuilder.Sql("ALTER TABLE tenant_roles ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE tenant_roles FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS tenant_isolation ON tenant_roles;
                CREATE POLICY tenant_isolation ON tenant_roles
                    FOR ALL
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

                DROP POLICY IF EXISTS subject_read ON tenant_roles;
                CREATE POLICY subject_read ON tenant_roles
                    FOR SELECT
                    USING (EXISTS (
                        SELECT 1 FROM tenant_members tm
                        WHERE tm.tenant_id = tenant_roles.tenant_id
                          AND tm.subject_id = NULLIF(current_setting('app.current_subject_id', true), '')::uuid));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS subject_read ON tenant_roles;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON tenant_roles;");
            migrationBuilder.Sql("ALTER TABLE tenant_roles NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE tenant_roles DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.Sql("DROP POLICY IF EXISTS subject_read ON tenant_member_roles;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON tenant_member_roles;");
            migrationBuilder.Sql("ALTER TABLE tenant_member_roles NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE tenant_member_roles DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.Sql("DROP POLICY IF EXISTS subject_read ON tenant_members;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON tenant_members;");
            migrationBuilder.Sql("ALTER TABLE tenant_members NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE tenant_members DISABLE ROW LEVEL SECURITY;");
        }
    }
}
