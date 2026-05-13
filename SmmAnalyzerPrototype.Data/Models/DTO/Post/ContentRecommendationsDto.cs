using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class ContentRecommendationsDto
    {
        [JsonPropertyName("textImprovements")]
        public List<string> TextImprovements { get; set; } = new();

        [JsonPropertyName("structuralChanges")]
        public List<string> StructuralChanges { get; set; } = new();

        [JsonPropertyName("topicIdeas")]
        public List<string> TopicIdeas { get; set; } = new();

        [JsonPropertyName("engagementBoosters")]
        public List<string> EngagementBoosters { get; set; } = new();

        [JsonPropertyName("overallAdvice")]
        public string OverallAdvice { get; set; } = string.Empty;
    }
}
