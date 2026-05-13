using SmmAnalyzerPrototype.Data.Models.DTO.Post;

namespace RAGTEST.Models
{
    public class ForecastPageModel
    {
        public Guid PostId { get; set; }
        public string CommunityName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        public DateTime? ForecastCheckedAt { get; set; }

        public EngagementForecastDto Result { get; set; }
        public string ErrorMessage { get; set; }
    }
}
