namespace SmmAnalyzerPrototype.Models
{
    public class AnalysisResult
    {
        public Guid PostId { get; set; }
        public List<ErrorDetail> GrammarErrors { get; set; } = new();
        public string StyleAssessment { get; set; } = string.Empty;
        public List<ProhibitedTopicMatch> ProhibitedTopics { get; set; } = new();
        public string EngagementForecast { get; set; } = string.Empty;
        public List<TopicRecommendation> Recommendations { get; set; } = new();
    }

    public class ErrorDetail
    {
        public string Type { get; set; } = string.Empty;
        public string Fragment { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
        public int Position { get; set; }
    }

    public class ProhibitedTopicMatch
    {
        public string Topic { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty;
        public string RegulationRef { get; set; } = string.Empty;
    }

    public class TopicRecommendation
    {
        public string Topic { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
