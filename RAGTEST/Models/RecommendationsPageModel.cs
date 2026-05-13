using SmmAnalyzerPrototype.Data.Models.DTO.Post;

namespace RAGTEST.Models
{
    public class RecommendationsPageModel
    {
        public Guid PostId { get; set; }
        public string CommunityName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        public DateTime? RecommendationsCheckedAt { get; set; }

        public ContentRecommendationsDto Result { get; set; }
        public string ErrorMessage { get; set; }
    }
}
