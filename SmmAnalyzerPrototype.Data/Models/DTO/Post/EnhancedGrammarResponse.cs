namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class EnhancedGrammarResponse
    {
        public List<GrammarErrorDto> RawErrors { get; set; } = new();
        public List<GrammarResultCardDto> Cards { get; set; } = new();
        public List<GrammarResultCardDto> SuspiciousCards { get; set; } = new();
    }
}