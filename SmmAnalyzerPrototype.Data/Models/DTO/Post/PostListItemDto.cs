namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class PostListItemDto
    {
        public Guid Id { get; set; }
        public string TextPreview { get; set; } = string.Empty;
        public string CommunityName { get; set; } = string.Empty;
        public string AuthorLogin { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? Status { get; set; }

        public bool GrammarChecked { get; set; }
        public bool StyleChecked { get; set; }
        public bool RegulationChecked { get; set; }
    }
}