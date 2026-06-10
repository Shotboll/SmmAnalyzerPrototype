using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class PostDetailsPageModel
    {
        public Guid PostId { get; set; }
        public Guid CommunityId { get; set; }
        public string CommunityName { get; set; } = string.Empty;
        public string AuthorLogin { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public DateTime? GrammarCheckedAt { get; set; }
        public DateTime? StyleCheckedAt { get; set; }
        public DateTime? RegulationCheckedAt { get; set; }
        public DateTime? ForecastCheckedAt { get; set; }
        public DateTime? RecommendationsCheckedAt { get; set; }

        public List<GrammarErrorDto> GrammarErrors { get; set; } = new();
        public List<ViolationDto> RegulationViolations { get; set; } = new();

        public string StyleAssessment { get; set; } = string.Empty;
        public string StyleSummary { get; set; } = string.Empty;

        public bool HasRegulationViolations { get; set; }
        public string RegulationComment { get; set; } = string.Empty;

        public string ForecastLevel { get; set; } = string.Empty;
        public int ForecastQualityScore { get; set; }
        public int ForecastLikes { get; set; }
        public int ForecastComments { get; set; }
        public int ForecastViews { get; set; }
        public double ForecastERPercent { get; set; }

        public List<string> RecommendationsText { get; set; } = new();
        public List<string> RecommendationsTopics { get; set; } = new();
        public string RecommendationsAdvice { get; set; } = string.Empty;
    }
}
