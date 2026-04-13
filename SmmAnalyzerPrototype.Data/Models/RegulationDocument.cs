using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmmAnalyzerPrototype.Data.Models
{
    [Table("regulation_documents")]
    public class RegulationDocument
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "text")]
        public string Content { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Category { get; set; }

        public Guid CommunityId { get; set; }

        [ForeignKey(nameof(CommunityId))]
        public virtual Community Community { get; set; } = null!;

        public virtual ICollection<RegulationChunk> Chunks { get; set; } = new List<RegulationChunk>();
    }
}