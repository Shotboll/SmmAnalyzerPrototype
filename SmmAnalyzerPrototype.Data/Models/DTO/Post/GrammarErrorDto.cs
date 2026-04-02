namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class GrammarErrorDto
    {
        public string Fragment { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Offset { get; set; }
        public int Length { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
