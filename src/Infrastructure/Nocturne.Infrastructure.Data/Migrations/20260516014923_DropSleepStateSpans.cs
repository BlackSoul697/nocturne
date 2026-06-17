using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropSleepStateSpans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sleep now lives in the dedicated sleep_sessions tables. The legacy
            // StateSpan rows held only a coarse start/end/state and cannot be
            // faithfully reshaped into stage-aware sessions, so they are dropped
            // rather than migrated. Sources re-sync sleep into the new tables.
            // FORCE ROW LEVEL SECURITY applies to the migrator, so the delete is
            // scoped per tenant with the tenant context set for each iteration.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    t RECORD;
                BEGIN
                    FOR t IN SELECT DISTINCT tenant_id FROM state_spans WHERE category = 'Sleep'
                    LOOP
                        PERFORM set_config('app.current_tenant_id', t.tenant_id::text, true);
                        DELETE FROM state_spans WHERE category = 'Sleep' AND tenant_id = t.tenant_id;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way data migration — dropped Sleep StateSpans cannot be restored.
        }
    }
}
