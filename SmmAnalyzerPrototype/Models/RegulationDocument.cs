namespace SmmAnalyzerPrototype.Models
{
    public class RegulationDocument
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public Guid CommunityId { get; set; }
        public Guid CreatedBy { get; set; }
    }
}
