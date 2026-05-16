using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrateSleepStateSpansToSleepSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    t RECORD;
                    grp RECORD;
                    session_id uuid;
                BEGIN
                    FOR t IN SELECT DISTINCT tenant_id FROM state_spans WHERE category = 'Sleep'
                    LOOP
                        PERFORM set_config('app.current_tenant_id', t.tenant_id::text, true);

                        FOR grp IN
                            SELECT
                                tenant_id,
                                date_trunc('day', start_timestamp) AS sleep_date,
                                MIN(start_timestamp) AS start_time,
                                MAX(COALESCE(end_timestamp, start_timestamp + interval '8 hours')) AS end_time
                            FROM state_spans
                            WHERE category = 'Sleep' AND tenant_id = t.tenant_id
                            GROUP BY tenant_id, date_trunc('day', start_timestamp)
                        LOOP
                            session_id := gen_random_uuid();

                            INSERT INTO sleep_sessions (
                                id, tenant_id, start_time, end_time, type, detection_method,
                                duration_ms, total_sleep_ms, source, original_id,
                                created_at, updated_at
                            ) VALUES (
                                session_id, grp.tenant_id, grp.start_time, grp.end_time,
                                'Unknown', 'Manual',
                                EXTRACT(EPOCH FROM (grp.end_time - grp.start_time))::bigint * 1000,
                                EXTRACT(EPOCH FROM (grp.end_time - grp.start_time))::bigint * 1000,
                                'Manual',
                                'migrated:' || grp.sleep_date::date::text,
                                NOW(), NOW()
                            );

                            INSERT INTO sleep_stages (id, tenant_id, sleep_session_id, start_time, end_time, stage, ordinal)
                            SELECT
                                gen_random_uuid(),
                                tenant_id,
                                session_id,
                                start_timestamp,
                                COALESCE(end_timestamp, start_timestamp + interval '8 hours'),
                                CASE
                                    WHEN LOWER(state) = 'deep' THEN 'Deep'
                                    WHEN LOWER(state) = 'rem' THEN 'Rem'
                                    WHEN LOWER(state) = 'light' THEN 'Light'
                                    WHEN LOWER(state) IN ('awake', 'wake') THEN 'Awake'
                                    ELSE 'Asleep'
                                END,
                                ROW_NUMBER() OVER (ORDER BY start_timestamp) - 1
                            FROM state_spans
                            WHERE category = 'Sleep'
                                AND tenant_id = grp.tenant_id
                                AND date_trunc('day', start_timestamp) = grp.sleep_date;
                        END LOOP;

                        DELETE FROM state_spans WHERE category = 'Sleep' AND tenant_id = t.tenant_id;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way data migration — Sleep StateSpans have been converted to SleepSessions/SleepStages.
        }
    }
}
