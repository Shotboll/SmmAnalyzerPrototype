using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmmAnalyzerPrototype.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_posts_users_AuthorId",
                table: "posts");

            migrationBuilder.DropForeignKey(
                name: "FK_posts_users_UserId",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_posts_AuthorId",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_posts_UserId",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AuthorId",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "posts");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "communities",
                type: "uuid",
                nullable: true);

            var defaultUserId = new Guid("11111111-1111-1111-1111-111111111111");

            migrationBuilder.Sql($@"
                INSERT INTO users (""Id"", ""Login"", ""PasswordHash"", ""Email"", created_at)
                VALUES (
                    '{defaultUserId}',
                    'test',
                    'AQAAAAIAAYagAAAAEJ8sJp0+qkQ6vV0rAKHx6u1n4j8E5J8/7qkq6R8m7yT0X4ZQ3xj8mFfYvN9J2ZkD9Q==',
                    'test@example.com',
                    NOW()
                )
                ON CONFLICT (""Login"") DO NOTHING;
            ");

            migrationBuilder.Sql($@"
                UPDATE communities
                SET ""UserId"" = '{defaultUserId}'
                WHERE ""UserId"" IS NULL;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "communities",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_communities_UserId",
                table: "communities",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_communities_users_UserId",
                table: "communities",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_communities_users_UserId",
                table: "communities");

            migrationBuilder.DropIndex(
                name: "IX_users_Email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_communities_UserId",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "communities");

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "AuthorId",
                table: "posts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "posts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_posts_AuthorId",
                table: "posts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_posts_UserId",
                table: "posts",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_posts_users_AuthorId",
                table: "posts",
                column: "AuthorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_posts_users_UserId",
                table: "posts",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id");
        }
    }
}
