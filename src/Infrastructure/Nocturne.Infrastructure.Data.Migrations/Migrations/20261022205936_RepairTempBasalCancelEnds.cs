using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Ends every temp basal the decomposer stored from a Temp Basal with no duration where it
    /// starts. Nightscout reads such a treatment as a cancel, and the decomposer now stores it so,
    /// but it used to store no end, which reads as a temp still running.
    /// </summary>
    /// <remarks>
    /// A row the decomposer writes carries a legacy id and a correlation id, and never a sync
    /// identifier. A connector's row carries no correlation id. A V4 create carries no legacy id, and
    /// a V4 sync upsert carries the sync identifier it matched on and matches only on one, so it never
    /// takes over a decomposed row. The predicate names all three columns, so a temp a client or a
    /// connector left open, which is still running, is left alone. A row ended at its start is not matched
    /// again, so running this twice changes nothing more, and <c>sys_updated_at</c> is left alone.
    /// FORCE ROW LEVEL SECURITY binds the migrator, hence the per-tenant GUC.
    /// </remarks>
    public partial class RepairTempBasalCancelEnds : Migration
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

                        UPDATE temp_basals SET end_timestamp = start_timestamp
                        WHERE tenant_id = t.id AND end_timestamp IS NULL
                          AND legacy_id IS NOT NULL AND correlation_id IS NOT NULL
                          AND sync_identifier IS NULL;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        /// <remarks>The end it set is the one the treatment always meant, so it is not cleared.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
