using Microsoft.EntityFrameworkCore;
using SmmAnalyzerPrototype.Models;

namespace SmmAnalyzerPrototype.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = default!;
        public DbSet<Community> Communities { get; set; } = default!;
        public DbSet<Post> Posts { get; set; } = default!;
        public DbSet<RegulationDocument> RegulationDocuments { get; set; } = default!;
    }
}
