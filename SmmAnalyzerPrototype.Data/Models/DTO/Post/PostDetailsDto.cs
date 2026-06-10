using SmmAnalyzerPrototype.Data.Enums;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class PostDetailsDto
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public Guid CommunityId { get; set; }
        public string CommunityName { get; set; } = string.Empty;
        public string AuthorLogin { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public PostStatus Status { get; set; }
        public string StatusText { get; set; } = string.Empty;

        public DateTime? GrammarCheckedAt { get; set; }
        public DateTime? StyleCheckedAt { get; set; }
        public DateTime? RegulationCheckedAt { get; set; }

        public string? StyleAssessment { get; set; }
        public string? StyleSummary { get; set; }
        public List<string> StyleStrengths { get; set; } = new();
        public List<string> StyleIssues { get; set; } = new();
        public List<string> StyleRecommendations { get; set; } = new();

        public bool? HasRegulationViolations { get; set; }
        public string? RegulationComment { get; set; }

        public List<GrammarErrorDto> GrammarErrors { get; set; } = new();
        public List<ViolationDto> RegulationViolations { get; set; } = new();

        public DateTime? ForecastCheckedAt { get; set; }
        public double ForecastConfidence { get; set; }
        public DateTime? RecommendationsCheckedAt { get; set; }
        public string? ForecastLevel { get; set; }
        public int ForecastLikes { get; set; }
        public int ForecastComments { get; set; }
        public int ForecastViews { get; set; }
        public double ForecastERPercent { get; set; }
        public string? ForecastReasoning { get; set; }
        public List<string> ForecastKeyFactors { get; set; } = new();
        public List<string> ForecastRisks { get; set; } = new();
        public string? ForecastComparison { get; set; }
        public int ForecastQualityScore { get; set; }

        public List<string> RecommendationsText { get; set; } = new();
        public List<string> RecommendationsStruct { get; set; } = new();
        public List<string> RecommendationsTopics { get; set; } = new();
        public List<string> RecommendationsBoosters { get; set; } = new();
        public string? RecommendationsAdvice { get; set; }
    }
}