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
        public string? Status { get; set; }

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
    }
}