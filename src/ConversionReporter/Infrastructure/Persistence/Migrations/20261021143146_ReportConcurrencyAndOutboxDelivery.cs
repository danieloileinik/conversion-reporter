using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConversionReporter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportConcurrencyAndOutboxDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_processed_at",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_actions_item_id_type",
                table: "actions");

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "reports",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<Guid>(
                name: "aggregate_id",
                table: "outbox_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "failed_at",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_attempt_at",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: DateTime.UnixEpoch);

            migrationBuilder.Sql("""
                UPDATE outbox_messages
                SET aggregate_id = COALESCE(
                    substring(payload FROM '"ReportId"[[:space:]]*:[[:space:]]*"([[:xdigit:]]{8}-[[:xdigit:]]{4}-[[:xdigit:]]{4}-[[:xdigit:]]{4}-[[:xdigit:]]{12})"'),
                    substring(payload FROM '"Id"[[:space:]]*:[[:space:]]*"([[:xdigit:]]{8}-[[:xdigit:]]{4}-[[:xdigit:]]{4}-[[:xdigit:]]{4}-[[:xdigit:]]{12})"'))::uuid
                WHERE aggregate_id IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_aggregate_id_created_at_id",
                table: "outbox_messages",
                columns: ["aggregate_id", "created_at", "id"],
                filter: "processed_at IS NULL AND failed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_next_attempt_at_created_at_id",
                table: "outbox_messages",
                columns: ["next_attempt_at", "created_at", "id"],
                filter: "processed_at IS NULL AND failed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_actions_item_id_created_at",
                table: "actions",
                columns: ["item_id", "created_at"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_aggregate_id_created_at_id",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_next_attempt_at_created_at_id",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_actions_item_id_created_at",
                table: "actions");

            migrationBuilder.DropColumn(
                name: "version",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "aggregate_id",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "failed_at",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                table: "outbox_messages",
                column: "processed_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_actions_item_id_type",
                table: "actions",
                columns: ["item_id", "type"]);
        }
    }
}
