using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class EngagementForecastDto
    {
        [JsonPropertyName("level")]
        public string Level { get; set; } = string.Empty;

        [JsonPropertyName("quality_score")]
        public int QualityScore { get; set; }

        [JsonPropertyName("expected_likes")]
        public int ExpectedLikes { get; set; }

        [JsonPropertyName("expected_comments")]
        public int ExpectedComments { get; set; }

        [JsonPropertyName("expected_views")]
        public int ExpectedViews { get; set; }

        [JsonPropertyName("expected_er_percent")]
        public double ExpectedERPercent { get; set; }

        public double Confidence { get; set; }
        public string Reasoning { get; set; } = string.Empty;

        [JsonPropertyName("key_factors")]
        public List<string> KeyFactors { get; set; } = new();

        public List<string> Risks { get; set; } = new();

        [JsonPropertyName("comparison_with_avg")]
        public string ComparisonWithAvg { get; set; } = string.Empty;
    }
}
