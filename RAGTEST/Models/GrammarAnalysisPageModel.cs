using SmmAnalyzerPrototype.Data.Models.DTO.Post;

namespace RAGTEST.Models
{
    public class GrammarAnalysisPageModel
    {
        public List<GrammarResultCardDto> Cards { get; set; } = new();
        public List<GrammarResultCardDto> SuspiciousCards { get; set; } = new();
    }
}
