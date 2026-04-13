using SmmAnalyzerPrototype.Data.Models.DTO.Post;

namespace RAGTEST.Models
{
    public class GrammarCheckPageModel
    {
        public Guid PostId { get; set; }
        public Guid CommunityId { get; set; }
        public string CommunityName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        public bool HasResult { get; set; }

        public List<GrammarResultCardDto> Cards { get; set; } = new();
        public List<GrammarResultCardDto> SuspiciousCards { get; set; } = new();
    }
}