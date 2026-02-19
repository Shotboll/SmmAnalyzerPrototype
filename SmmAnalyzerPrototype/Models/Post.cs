namespace SmmAnalyzerPrototype.Models
{
    public class Post
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public PostStatus Status { get; set; }
        public Guid AuthorId { get; set; }
        public Guid CommunityId { get; set; }
    }

    public enum PostStatus
    {
        Draft,
        Approved,
        Rejected
    }
}
