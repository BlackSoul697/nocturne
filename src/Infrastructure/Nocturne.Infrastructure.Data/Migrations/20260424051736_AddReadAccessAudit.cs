using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReadAccessAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_default",
                table: "tenants");

            migrationBuilder.CreateTable(
                name: "coach_mark_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mark_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coach_mark_states", x => x.id);
                    table.ForeignKey(
                        name: "FK_coach_mark_states_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "read_access_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    auth_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    api_secret_hash_prefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    endpoint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    record_count = table.Column<int>(type: "integer", nullable: true),
                    query_parameters = table.Column<string>(type: "jsonb", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_read_access_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_read_access_log_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_audit_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    read_audit_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    read_audit_retention_days = table.Column<int>(type: "integer", nullable: true),
                    mutation_audit_retention_days = table.Column<int>(type: "integer", nullable: true),
                    sys_created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    sys_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_audit_config", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_audit_config_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_coach_mark_states_subject_id_mark_key",
                table: "coach_mark_states",
                columns: new[] { "subject_id", "mark_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coach_mark_states_tenant_id",
                table: "coach_mark_states",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_read_access_log_correlation",
                table: "read_access_log",
                column: "correlation_id",
                filter: "correlation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_read_access_log_created",
                table: "read_access_log",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_read_access_log_entity_type",
                table: "read_access_log",
                columns: new[] { "tenant_id", "entity_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_read_access_log_subject",
                table: "read_access_log",
                columns: new[] { "tenant_id", "subject_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_audit_config_tenant_id",
                table: "tenant_audit_config",
                column: "tenant_id",
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE read_access_log ENABLE ROW LEVEL SECURITY;
                ALTER TABLE read_access_log FORCE ROW LEVEL SECURITY;

                CREATE POLICY tenant_isolation ON read_access_log
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coach_mark_states");

            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON read_access_log;");

            migrationBuilder.DropTable(
                name: "read_access_log");

            migrationBuilder.DropTable(
                name: "tenant_audit_config");

            migrationBuilder.AddColumn<bool>(
                name: "is_default",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
