using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Deletes the Profile spans <c>AddProfileActivations</c> converted and left in place, with their
    /// <c>linked_records</c> rows, and the <c>@@@@@</c> settings and schedule rows a Profile Switch's
    /// <c>profileJson</c> was decomposed into. Each converted span's id is now its activation's, so
    /// the rows are deleted rather than soft-deleted: a tombstone under that id is a delete a history
    /// read could deliver for a record still served, and a tombstoned <c>@@@@@</c> settings row would
    /// reach v3 profile history as a deleted document. FORCE ROW LEVEL SECURITY binds the migrator,
    /// hence the per-tenant GUC; each statement names the tenant too, so a role that bypasses RLS
    /// still deletes each tenant's rows once.
    /// </summary>
    public partial class RemoveLegacyProfileSpansAndSwitchSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    t RECORD;
                BEGIN
                    FOR t IN SELECT id FROM tenants LOOP
                        PERFORM set_config('app.current_tenant_id', t.id::text, true);

                        DELETE FROM linked_records l
                        USING state_spans s
                        WHERE l.tenant_id = t.id
                          AND s.tenant_id = t.id
                          AND s.category = 'Profile'
                          AND l.record_type = 'statespan'
                          AND l.record_id = s.id;

                        DELETE FROM state_spans
                        WHERE tenant_id = t.id
                          AND category = 'Profile';

                        DELETE FROM therapy_settings
                        WHERE tenant_id = t.id
                          AND strpos(profile_name, '@@@@@') > 0;

                        DELETE FROM basal_schedules
                        WHERE tenant_id = t.id
                          AND strpos(profile_name, '@@@@@') > 0;

                        DELETE FROM carb_ratio_schedules
                        WHERE tenant_id = t.id
                          AND strpos(profile_name, '@@@@@') > 0;

                        DELETE FROM sensitivity_schedules
                        WHERE tenant_id = t.id
                          AND strpos(profile_name, '@@@@@') > 0;

                        DELETE FROM target_range_schedules
                        WHERE tenant_id = t.id
                          AND strpos(profile_name, '@@@@@') > 0;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        /// <remarks>The deleted rows cannot be restored; their profile switches live on as activations.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
