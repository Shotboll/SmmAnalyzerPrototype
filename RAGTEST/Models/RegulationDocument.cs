using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RAGTEST.Models
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
        [MaxLength(255)]
        public string Content { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Category { get; set; }

        public Guid CommunityId { get; set; }

        [ForeignKey("CommunityId")]
        public virtual Community Community { get; set; } = null!;
    }
}
