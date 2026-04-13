using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmmAnalyzerPrototype.Data.Models
{
    [Table("analysis_results")]
    public class AnalysisResult
    {
        [Key]
        [Column("post_id")]
        public Guid PostId { get; set; }

        [ForeignKey(nameof(PostId))]
        public virtual Post Post { get; set; } = null!;

        [Column("grammar_checked_at")]
        public DateTime? GrammarCheckedAt { get; set; }

        [Column("style_checked_at")]
        public DateTime? StyleCheckedAt { get; set; }

        [Column("regulation_checked_at")]
        public DateTime? RegulationCheckedAt { get; set; }

        [MaxLength(100)]
        [Column("style_assessment")]
        public string? StyleAssessment { get; set; }

        [Column("style_summary", TypeName = "text")]
        public string? StyleSummary { get; set; }

        [Column("style_strengths_json", TypeName = "text")]
        public string? StyleStrengthsJson { get; set; }

        [Column("style_issues_json", TypeName = "text")]
        public string? StyleIssuesJson { get; set; }

        [Column("style_recommendations_json", TypeName = "text")]
        public string? StyleRecommendationsJson { get; set; }

        [Column("has_regulation_violations")]
        public bool? HasRegulationViolations { get; set; }

        [Column("regulation_comment", TypeName = "text")]
        public string? RegulationComment { get; set; }

        [MaxLength(100)]
        [Column("engagement_forecast")]
        public string? EngagementForecast { get; set; }

        [Column("recommendations_json", TypeName = "text")]
        public string? RecommendationsJson { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<GrammarError> GrammarErrors { get; set; } = new List<GrammarError>();
        public virtual ICollection<ProhibitedTopicMatch> ProhibitedTopicMatches { get; set; } = new List<ProhibitedTopicMatch>();
    }
}