using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmmAnalyzerPrototype.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVkFieldsToCommunity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "VkGroupId",
                table: "communities",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VkInput",
                table: "communities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VkPostsSyncedAt",
                table: "communities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VkScreenName",
                table: "communities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VkUrl",
                table: "communities",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VkGroupId",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "VkInput",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "VkPostsSyncedAt",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "VkScreenName",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "VkUrl",
                table: "communities");
        }
    }
}
