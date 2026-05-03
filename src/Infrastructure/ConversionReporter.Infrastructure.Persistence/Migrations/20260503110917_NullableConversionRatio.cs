using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConversionReporter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NullableConversionRatio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "ratio",
                table: "reports",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "ratio",
                table: "reports",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);
        }
    }
}
