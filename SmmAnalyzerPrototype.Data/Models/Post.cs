using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmmAnalyzerPrototype.Data.Models
{
    [Table("posts")]
    public class Post
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column(TypeName = "text")]
        [MaxLength(5000)]
        public string Text { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Draft";

        public Guid AuthorId { get; set; }

        [ForeignKey(nameof(AuthorId))]
        public virtual User Author { get; set; } = null!;

        public Guid CommunityId { get; set; }

        [ForeignKey(nameof(CommunityId))]
        public virtual Community Community { get; set; } = null!;

        public virtual AnalysisResult? AnalysisResult { get; set; }
    }
}