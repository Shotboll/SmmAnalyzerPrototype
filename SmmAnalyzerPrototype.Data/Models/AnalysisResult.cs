using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmmAnalyzerPrototype.Data.Models
{
    [Table("analysis_results")]
    public class AnalysisResult
    {
        [Key]
        public Guid PostId { get; set; }

        public int? GrammarErrors { get; set; }

        [MaxLength(255)]
        public string? StyleAssessment { get; set; }

        [MaxLength(255)]
        public string? ProhibitedTopics { get; set; }

        [MaxLength(255)]
        public string? EngagementForecast { get; set; }

        [MaxLength(255)]
        public string? Recommendations { get; set; }

        [ForeignKey("PostId")]
        public virtual Post Post { get; set; } = null!;
    }
}
