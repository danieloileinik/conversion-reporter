using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConversionReporter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "processed_commands",
                columns: table => new
                {
                    command_type = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    response = table.Column<string>(type: "text", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processed_commands", x => new { x.command_type, x.idempotency_key });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_commands");
        }
    }
}
