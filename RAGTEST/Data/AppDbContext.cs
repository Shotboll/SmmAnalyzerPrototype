using Microsoft.EntityFrameworkCore;
using RAGTEST.Models;

namespace RAGTEST.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Community> Communities { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<AnalysisResult> AnalysisResults { get; set; }
        public DbSet<CommunityPost> CommunityPosts { get; set; }
        public DbSet<RegulationDocument> RegulationDocuments { get; set; }
        public DbSet<RegulationChunk> RegulationChunks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<RegulationChunk>(entity =>
            {
                entity.Property(e => e.Embedding)
                      .HasColumnType("vector(1024)");
            });

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Login)
                .IsUnique();

            modelBuilder.Entity<AnalysisResult>()
                .HasOne(ar => ar.Post)
                .WithOne()
                .HasForeignKey<AnalysisResult>(ar => ar.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
