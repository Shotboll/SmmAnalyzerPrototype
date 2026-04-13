namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class ViolationDto
    {
        public int RuleNumber { get; set; }
        public string RuleShort { get; set; } = string.Empty;
        public string MatchedText { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;

    }
}