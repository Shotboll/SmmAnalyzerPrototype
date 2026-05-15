using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmmAnalyzerPrototype.Data.Models
{
    [Table("communities")]
    public class Community
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? TargetAudience { get; set; }

        [MaxLength(255)]
        public string? StyleProfile { get; set; }

        public string? VkInput { get; set; }

        public long? VkGroupId { get; set; }

        public string? VkScreenName { get; set; }

        public string? VkUrl { get; set; }

        public DateTime? VkPostsSyncedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;

        public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
        public virtual ICollection<CommunityPost> CommunityPosts { get; set; } = new List<CommunityPost>();
        public virtual ICollection<RegulationDocument> RegulationDocuments { get; set; } = new List<RegulationDocument>();
    }
}