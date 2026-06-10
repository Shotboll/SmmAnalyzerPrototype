using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmmAnalyzerPrototype.Data.Migrations
{
    /// <inheritdoc />
    public partial class PostMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
    ALTER TABLE posts
    ALTER COLUMN "Status" TYPE integer
    USING CASE
        WHEN "Status" = 'Draft' THEN 1
        WHEN "Status" = 'Черновик' THEN 1
        WHEN "Status" = 'PartiallyAnalyzed' THEN 2
        WHEN "Status" = 'Частично проанализирован' THEN 2
        WHEN "Status" = 'Analyzed' THEN 3
        WHEN "Status" = 'Проанализирован' THEN 3
        ELSE 1
    END;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
        ALTER TABLE posts
        ALTER COLUMN "Status" TYPE text
        USING CASE
            WHEN "Status" = 1 THEN 'Draft'
            WHEN "Status" = 2 THEN 'PartiallyAnalyzed'
            WHEN "Status" = 3 THEN 'Analyzed'
            ELSE 'Draft'
        END;
    """);
        }
    }
}
