namespace RAGTEST.Models.VkModels
{
    public class VkCommentDto
    {
        public long Id { get; set; }
        public string Text { get; set; }
        public DateTime Date { get; set; }
        public int Likes { get; set; }
        public long FromId { get; set; }
    }
}
