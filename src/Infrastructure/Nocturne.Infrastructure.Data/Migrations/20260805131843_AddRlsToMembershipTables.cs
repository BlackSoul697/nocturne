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
        /// <c>tenant_members</c> is read on the authentication hot path of every request, so its
        /// USING clause carries a second arm: a subject reaches their own memberships across every
        /// tenant under <c>app.current_subject_id</c>. That arm is read reach only — WITH CHECK
        /// stays tenant-pinned, so a subject can never write a membership anywhere. It exists for
        /// the genuinely cross-tenant reads: the tenant switcher, the caregiver overview, and the
        /// passkey enrolment probe, whose anti-join must see a membership held in another tenant or
        /// it would enrol a credential onto somebody else's account.
        /// </para>
        /// <para>
        /// The other two tables inherit their visibility rather than restating it. PostgreSQL
        /// evaluates a policy expression as the querying role and applies RLS to the tables that
        /// expression references, so the EXISTS on <c>tenant_members</c> below is itself filtered by
        /// the tenant_members policy — both of its arms. That makes membership the single source of
        /// truth: widening or narrowing it propagates, and the restrictive <c>share_category_read</c>
        /// policy the startup reconciler puts on tenant_members (unclassified, so shares are denied)
        /// denies tenant_member_roles too without it needing a policy of its own. tenant_roles
        /// chains one step further, through tenant_member_roles, which is why a role a subject holds
        /// in another tenant is readable under the subject GUC while an unrelated tenant's roles
        /// are not.
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
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
                        OR subject_id = NULLIF(current_setting('app.current_subject_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("ALTER TABLE tenant_member_roles ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE tenant_member_roles FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS tenant_isolation ON tenant_member_roles;
                CREATE POLICY tenant_isolation ON tenant_member_roles
                    USING (EXISTS (
                        SELECT 1 FROM tenant_members tm
                        WHERE tm.id = tenant_member_roles.tenant_member_id))
                    WITH CHECK (EXISTS (
                        SELECT 1 FROM tenant_members tm
                        WHERE tm.id = tenant_member_roles.tenant_member_id
                          AND tm.tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid));
                """);

            migrationBuilder.Sql("ALTER TABLE tenant_roles ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE tenant_roles FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS tenant_isolation ON tenant_roles;
                CREATE POLICY tenant_isolation ON tenant_roles
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
                        OR EXISTS (
                            SELECT 1 FROM tenant_member_roles tmr
                            WHERE tmr.tenant_role_id = tenant_roles.id))
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON tenant_roles;");
            migrationBuilder.Sql("ALTER TABLE tenant_roles NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE tenant_roles DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON tenant_member_roles;");
            migrationBuilder.Sql("ALTER TABLE tenant_member_roles NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE tenant_member_roles DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON tenant_members;");
            migrationBuilder.Sql("ALTER TABLE tenant_members NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE tenant_members DISABLE ROW LEVEL SECURITY;");
        }
    }
}
