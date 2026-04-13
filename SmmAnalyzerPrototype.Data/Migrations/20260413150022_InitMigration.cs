using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace SmmAnalyzerPrototype.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "communities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TargetAudience = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StyleProfile = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Login = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Likes = table.Column<int>(type: "integer", nullable: false),
                    Comments = table.Column<int>(type: "integer", nullable: false),
                    Reposts = table.Column<int>(type: "integer", nullable: false),
                    Views = table.Column<int>(type: "integer", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_posts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_posts_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regulation_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulation_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regulation_documents_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "posts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_posts_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_posts_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_posts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "regulation_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulationId = table.Column<Guid>(type: "uuid", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    chunk_text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulation_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regulation_chunks_regulation_documents_RegulationId",
                        column: x => x.RegulationId,
                        principalTable: "regulation_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_results",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grammar_checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    style_checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    regulation_checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    style_assessment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    style_summary = table.Column<string>(type: "text", nullable: true),
                    style_strengths_json = table.Column<string>(type: "text", nullable: true),
                    style_issues_json = table.Column<string>(type: "text", nullable: true),
                    style_recommendations_json = table.Column<string>(type: "text", nullable: true),
                    has_regulation_violations = table.Column<bool>(type: "boolean", nullable: true),
                    regulation_comment = table.Column<string>(type: "text", nullable: true),
                    engagement_forecast = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    recommendations_json = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_results", x => x.post_id);
                    table.ForeignKey(
                        name: "FK_analysis_results_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grammar_errors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fragment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Suggestion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    is_suspicious = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grammar_errors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grammar_errors_analysis_results_AnalysisResultId",
                        column: x => x.AnalysisResultId,
                        principalTable: "analysis_results",
                        principalColumn: "post_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prohibited_topic_matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    Topic = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: true),
                    regulation_ref = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Explanation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prohibited_topic_matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prohibited_topic_matches_analysis_results_AnalysisResultId",
                        column: x => x.AnalysisResultId,
                        principalTable: "analysis_results",
                        principalColumn: "post_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_posts_CommunityId",
                table: "community_posts",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_grammar_errors_AnalysisResultId",
                table: "grammar_errors",
                column: "AnalysisResultId");

            migrationBuilder.CreateIndex(
                name: "IX_posts_AuthorId",
                table: "posts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_posts_CommunityId",
                table: "posts",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_posts_UserId",
                table: "posts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_prohibited_topic_matches_AnalysisResultId",
                table: "prohibited_topic_matches",
                column: "AnalysisResultId");

            migrationBuilder.CreateIndex(
                name: "IX_regulation_chunks_RegulationId",
                table: "regulation_chunks",
                column: "RegulationId");

            migrationBuilder.CreateIndex(
                name: "IX_regulation_documents_CommunityId",
                table: "regulation_documents",
                column: "CommunityId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Login",
                table: "users",
                column: "Login",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_posts");

            migrationBuilder.DropTable(
                name: "grammar_errors");

            migrationBuilder.DropTable(
                name: "prohibited_topic_matches");

            migrationBuilder.DropTable(
                name: "regulation_chunks");

            migrationBuilder.DropTable(
                name: "analysis_results");

            migrationBuilder.DropTable(
                name: "regulation_documents");

            migrationBuilder.DropTable(
                name: "posts");

            migrationBuilder.DropTable(
                name: "communities");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
