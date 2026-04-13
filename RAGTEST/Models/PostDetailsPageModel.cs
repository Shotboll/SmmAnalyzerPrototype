using SmmAnalyzerPrototype.Data.Models;

namespace RAGTEST.Models
{
    public class PostDetailsPageModel
    {
        public Post Post { get; set; } = null!;
        public AnalysisResult? AnalysisResult { get; set; }

        public List<GrammarError> GrammarErrors { get; set; } = new();
        public List<ProhibitedTopicMatch> ProhibitedTopicMatches { get; set; } = new();

        public List<string> StyleStrengths { get; set; } = new();
        public List<string> StyleIssues { get; set; } = new();
        public List<string> StyleRecommendations { get; set; } = new();
    }
}