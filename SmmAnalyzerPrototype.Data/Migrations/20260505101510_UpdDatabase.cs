using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmmAnalyzerPrototype.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_community_posts_CommunityId",
                table: "community_posts");

            migrationBuilder.AddColumn<long>(
                name: "vk_post_id",
                table: "community_posts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_community_posts_community_vk",
                table: "community_posts",
                columns: new[] { "CommunityId", "vk_post_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_community_posts_community_vk",
                table: "community_posts");

            migrationBuilder.DropColumn(
                name: "vk_post_id",
                table: "community_posts");

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_CommunityId",
                table: "community_posts",
                column: "CommunityId");
        }
    }
}
