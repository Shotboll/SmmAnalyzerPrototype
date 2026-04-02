namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class AnalyzePostRequest
    {
        public string Text { get; set; } = string.Empty;
        public Guid CommunityId { get; set; }
    }
}
