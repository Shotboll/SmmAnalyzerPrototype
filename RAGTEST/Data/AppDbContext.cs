using Microsoft.EntityFrameworkCore;
using RAGTEST.Models;

namespace RAGTEST.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<RegulationChunk> RegulationChunks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<RegulationChunk>(entity =>
            {
                entity.Property(e => e.Embedding)
                      .HasColumnType("vector(1024)"); // или vector без размера, если не фиксирован
            });
        }
    }
}
