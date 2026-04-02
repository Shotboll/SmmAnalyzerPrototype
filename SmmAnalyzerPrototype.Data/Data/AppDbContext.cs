using Microsoft.EntityFrameworkCore;
using SmmAnalyzerPrototype.Data.Models;

namespace SmmAnalyzerPrototype.Data.Data
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

            modelBuilder.Entity<RegulationDocument>()
                .HasMany(d => d.Chunks)
                .WithOne(c => c.Regulation)
                .HasForeignKey(c => c.RegulationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
