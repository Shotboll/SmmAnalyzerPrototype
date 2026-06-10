using SmmAnalyzerPrototype.Data.Enums;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class PostListItemDto
    {
        public Guid Id { get; set; }
        public string TextPreview { get; set; } = string.Empty;
        public string CommunityName { get; set; } = string.Empty;
        public string AuthorLogin { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public PostStatus Status { get; set; }
        public string StatusText { get; set; } = string.Empty;

        public bool GrammarChecked { get; set; }
        public bool StyleChecked { get; set; }
        public bool RegulationChecked { get; set; }
        public bool ForecastChecked { get; set; }
        public bool RecommendationsChecked { get; set; }
    }
}