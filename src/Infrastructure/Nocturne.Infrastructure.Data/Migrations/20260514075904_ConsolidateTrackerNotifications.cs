using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateTrackerNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tracker_instances_user_completed",
                table: "tracker_instances");

            migrationBuilder.DropIndex(
                name: "ix_tracker_instances_user_id",
                table: "tracker_instances");

            migrationBuilder.DropIndex(
                name: "ix_tracker_definitions_user_category",
                table: "tracker_definitions");

            migrationBuilder.DropIndex(
                name: "ix_tracker_definitions_user_id",
                table: "tracker_definitions");

            migrationBuilder.DropColumn(
                name: "audio_enabled",
                table: "tracker_notification_thresholds");

            migrationBuilder.DropColumn(
                name: "audio_sound",
                table: "tracker_notification_thresholds");

            migrationBuilder.DropColumn(
                name: "max_repeats",
                table: "tracker_notification_thresholds");

            migrationBuilder.DropColumn(
                name: "push_enabled",
                table: "tracker_notification_thresholds");

            migrationBuilder.DropColumn(
                name: "repeat_interval_mins",
                table: "tracker_notification_thresholds");

            migrationBuilder.DropColumn(
                name: "respect_quiet_hours",
                table: "tracker_notification_thresholds");

            migrationBuilder.DropColumn(
                name: "vibrate_enabled",
                table: "tracker_notification_thresholds");

            migrationBuilder.DropColumn(
                name: "ack_snooze_mins",
                table: "tracker_instances");

            migrationBuilder.DropColumn(
                name: "last_acked_at",
                table: "tracker_instances");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "tracker_instances");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "tracker_definitions");

            migrationBuilder.AddColumn<string>(
                name: "source_template",
                table: "alert_rules",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source_template",
                table: "alert_rules");

            migrationBuilder.AddColumn<bool>(
                name: "audio_enabled",
                table: "tracker_notification_thresholds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "audio_sound",
                table: "tracker_notification_thresholds",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_repeats",
                table: "tracker_notification_thresholds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "push_enabled",
                table: "tracker_notification_thresholds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "repeat_interval_mins",
                table: "tracker_notification_thresholds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "respect_quiet_hours",
                table: "tracker_notification_thresholds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "vibrate_enabled",
                table: "tracker_notification_thresholds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ack_snooze_mins",
                table: "tracker_instances",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_acked_at",
                table: "tracker_instances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "user_id",
                table: "tracker_instances",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "user_id",
                table: "tracker_definitions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_tracker_instances_user_completed",
                table: "tracker_instances",
                columns: new[] { "user_id", "completed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tracker_instances_user_id",
                table: "tracker_instances",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracker_definitions_user_category",
                table: "tracker_definitions",
                columns: new[] { "user_id", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_tracker_definitions_user_id",
                table: "tracker_definitions",
                column: "user_id");
        }
    }
}
