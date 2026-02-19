using System.ComponentModel.DataAnnotations;

namespace SmmAnalyzerPrototype.Models
{
    public class RegulationCreateModel
    {
        [Required] public string Title { get; set; } = string.Empty;
        [Required] public string Category { get; set; } = string.Empty;
        [Required] public string Content { get; set; } = string.Empty;
        [Required] public Guid CommunityId { get; set; }
    }
}
