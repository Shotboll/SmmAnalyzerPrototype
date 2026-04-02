using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmmAnalyzerPrototype.Data.Models
{
    [Table("community_posts")]
    public class CommunityPost
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(50)]
        public string Source { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "text")]
        public string Text { get; set; } = string.Empty;

        [Column("published_at")]
        public DateTime PublishedAt { get; set; }

        public int Likes { get; set; }
        public int Comments { get; set; }
        public int Reposts { get; set; }
        public int Views { get; set; }

        public Guid CommunityId { get; set; }

        [ForeignKey("CommunityId")]
        public virtual Community Community { get; set; } = null!;
    }
}
