using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveMigratedSecretsOfNonMembersToDeviceSubject : Migration
    {
        /// <summary>
        /// An api-secret authenticates only while its grant's subject is a member of the tenant. A
        /// Nightscout secret adopted on behalf of someone who is not (a platform admin who saved the
        /// connector, or a member since removed) would stop every uploader using it, so those grants
        /// move to the tenant's device subject, created where a tenant has none. See
        /// <c>DeviceSubjectFilter</c> for why the holder is not a person.
        /// </summary>
        /// <remarks>
        /// Only adopted secrets move. A minted <c>noc_</c> grant also carries a
        /// <c>legacy_secret_hash</c>, but it belongs to the person it was issued to and stops with
        /// their membership. Only <c>oauth_grants</c> is tenant-scoped, hence the per-tenant GUC.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    r RECORD;
                    device_subject_id uuid;
                BEGIN
                    FOR r IN SELECT id AS tenant_id FROM tenants
                    LOOP
                        PERFORM set_config('app.current_tenant_id', r.tenant_id::text, true);

                        CREATE TEMP TABLE orphaned_secrets AS
                            SELECT g.id
                              FROM oauth_grants g
                             WHERE g.tenant_id = r.tenant_id
                               AND g.grant_type = 'direct'
                               AND g.is_migrated
                               AND g.legacy_secret_hash IS NOT NULL
                               AND g.revoked_at IS NULL
                               AND NOT EXISTS (
                                     SELECT 1 FROM tenant_members tm
                                      WHERE tm.tenant_id = r.tenant_id
                                        AND tm.subject_id = g.subject_id);

                        IF NOT EXISTS (SELECT 1 FROM orphaned_secrets) THEN
                            DROP TABLE orphaned_secrets;
                            CONTINUE;
                        END IF;

                        device_subject_id := NULL;

                        SELECT s.id INTO device_subject_id
                          FROM tenant_members tm
                          JOIN subjects s ON s.id = tm.subject_id
                         WHERE tm.tenant_id = r.tenant_id
                           AND s.is_system_subject
                           AND s.name = 'Devices'
                         LIMIT 1;

                        IF device_subject_id IS NULL THEN
                            device_subject_id := gen_random_uuid();

                            INSERT INTO subjects (
                                id, name, notes, is_active, is_system_subject,
                                created_at, updated_at, approval_status)
                            VALUES (
                                device_subject_id, 'Devices',
                                'Holds the API tokens for this site. The scopes on each token '
                                  || 'decide what it can do.',
                                true, true, now(), now(), 'Approved');

                            INSERT INTO tenant_members (
                                id, tenant_id, subject_id, direct_permissions,
                                sys_created_at, sys_updated_at, limit_to_24_hours)
                            VALUES (
                                gen_random_uuid(), r.tenant_id, device_subject_id,
                                '["*"]'::jsonb, now(), now(), false);
                        END IF;

                        UPDATE oauth_grants
                           SET subject_id = device_subject_id
                         WHERE id IN (SELECT id FROM orphaned_secrets);

                        DROP TABLE orphaned_secrets;
                    END LOOP;
                END $$;
                """);
        }

        /// <remarks>
        /// Not reversed: the grants' previous holders are not recorded, and putting a secret back on
        /// a non-member would only make it refuse every request again.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
