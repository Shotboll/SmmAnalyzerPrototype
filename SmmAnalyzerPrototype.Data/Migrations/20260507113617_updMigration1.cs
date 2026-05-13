using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmmAnalyzerPrototype.Data.Migrations
{
    /// <inheritdoc />
    public partial class updMigration1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "engagement_forecast",
                table: "analysis_results",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "forecast_checked_at",
                table: "analysis_results",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "recommendations_checked_at",
                table: "analysis_results",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "forecast_checked_at",
                table: "analysis_results");

            migrationBuilder.DropColumn(
                name: "recommendations_checked_at",
                table: "analysis_results");

            migrationBuilder.AlterColumn<string>(
                name: "engagement_forecast",
                table: "analysis_results",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
