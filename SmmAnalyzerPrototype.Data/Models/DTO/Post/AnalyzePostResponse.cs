namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class AnalyzePostResponse
    {
        public bool HasViolations { get; set; }
        public List<ViolationDto> Violations { get; set; } = new();
        public string Comment { get; set; } = string.Empty;
    }
}
