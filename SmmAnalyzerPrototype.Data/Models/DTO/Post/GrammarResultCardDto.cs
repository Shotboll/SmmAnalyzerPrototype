namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class GrammarResultCardDto
    {
        public string Original { get; set; } = string.Empty;
        public string Correction { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
        public int Offset { get; set; }
        public int Length { get; set; }
        public bool IsSuspicious { get; set; }
    }
}