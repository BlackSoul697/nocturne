using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "translation_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    msgctxt = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    msgid = table.Column<string>(type: "text", nullable: false),
                    translations = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_drafts", x => x.id);
                    table.ForeignKey(
                        name: "FK_translation_drafts_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_translation_drafts_subject_id_locale",
                table: "translation_drafts",
                columns: new[] { "subject_id", "locale" });

            migrationBuilder.CreateIndex(
                name: "IX_translation_drafts_tenant_id",
                table: "translation_drafts",
                column: "tenant_id");

            // The logical key includes unbounded msgid text, which cannot be
            // a plain btree column; hash it. Raw SQL because EF cannot model
            // expression indexes.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_translation_drafts_logical_key
                    ON translation_drafts (subject_id, locale, msgctxt, md5(msgid));
                """);

            migrationBuilder.Sql("ALTER TABLE translation_drafts ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE translation_drafts FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                """
                CREATE POLICY tenant_isolation ON translation_drafts
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON translation_drafts;");
            migrationBuilder.Sql("ALTER TABLE translation_drafts NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE translation_drafts DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.DropTable(
                name: "translation_drafts");
        }
    }
}
