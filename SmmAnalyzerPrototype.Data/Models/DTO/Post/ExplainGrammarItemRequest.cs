namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class ExplainGrammarItemRequest
    {
        public string Fragment { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public string Sentence { get; set; } = string.Empty;
    }
}