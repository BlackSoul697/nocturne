using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RetireUiSettingsAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Earlier versions stored the whole UI settings document in ui:settings:complete, and
            // reads fell back to it for any section without a row of its own. Each section it holds
            // moves to the row UISettingsService reads, unless that row already serves a value (an
            // active row whose value is neither SQL nor JSON null), less the properties that row
            // never carries; the alarm configuration moves to its own row only when neither the
            // notifications row nor the alarms row served one. An aggregate that is not a JSON
            // object fails every read today and is dropped. FORCE ROW LEVEL SECURITY binds the
            // migrator too, hence the per-tenant GUC.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    t RECORD;
                    raw text;
                    doc jsonb;
                    section text;
                    part jsonb;
                    alarms jsonb;
                    stamp bigint := (extract(epoch FROM now()) * 1000)::bigint;
                BEGIN
                    FOR t IN SELECT id FROM tenants LOOP
                        PERFORM set_config('app.current_tenant_id', t.id::text, true);

                        raw := NULL;
                        SELECT value INTO raw
                        FROM settings
                        WHERE tenant_id = t.id AND key = 'ui:settings:complete' AND is_active;

                        BEGIN
                            doc := raw::jsonb;
                        EXCEPTION WHEN invalid_text_representation THEN
                            doc := NULL;
                        END;

                        IF jsonb_typeof(doc) = 'object' THEN
                            FOREACH section IN ARRAY ARRAY['devices', 'algorithm', 'features', 'notifications', 'services', 'dataQuality'] LOOP
                                part := doc -> section;
                                CONTINUE WHEN jsonb_typeof(part) IS DISTINCT FROM 'object';
                                CONTINUE WHEN EXISTS (
                                    SELECT 1 FROM settings
                                    WHERE tenant_id = t.id AND key = 'ui:settings:' || lower(section)
                                      AND is_active AND value IS NOT NULL AND btrim(value) <> 'null');

                                IF section = 'notifications' THEN
                                    alarms := part -> 'alarmConfiguration';
                                    IF jsonb_typeof(alarms) = 'object' AND NOT EXISTS (
                                        SELECT 1 FROM settings
                                        WHERE tenant_id = t.id AND key = 'ui:settings:notifications:alarms'
                                          AND is_active AND value IS NOT NULL AND btrim(value) <> 'null') THEN
                                        INSERT INTO settings (id, tenant_id, key, value, mills, srv_created, srv_modified, is_active, notes, app)
                                        VALUES (gen_random_uuid(), t.id, 'ui:settings:notifications:alarms', alarms::text,
                                                stamp, now(), now(), true, 'xDrip+-style alarm profiles configuration', 'nocturne-api')
                                        ON CONFLICT (tenant_id, key) DO UPDATE
                                        SET value = EXCLUDED.value, mills = EXCLUDED.mills, srv_modified = EXCLUDED.srv_modified, is_active = true;
                                    END IF;
                                    part := part - 'alarmConfiguration';
                                ELSIF section = 'services' THEN
                                    part := part - 'availableServices';
                                END IF;

                                INSERT INTO settings (id, tenant_id, key, value, mills, srv_created, srv_modified, is_active, notes, app)
                                VALUES (gen_random_uuid(), t.id, 'ui:settings:' || lower(section), part::text,
                                        stamp, now(), now(), true, 'UI settings section: ' || section, 'nocturne-api')
                                ON CONFLICT (tenant_id, key) DO UPDATE
                                SET value = EXCLUDED.value, mills = EXCLUDED.mills, srv_modified = EXCLUDED.srv_modified, is_active = true;
                            END LOOP;
                        END IF;

                        DELETE FROM settings WHERE tenant_id = t.id AND key = 'ui:settings:complete';
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
