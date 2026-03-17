namespace RAGTEST.Models.VkModels
{
    public class VkPostDto
    {
        public long Id { get; set; }
        public string Text { get; set; }
        public DateTime Date { get; set; }
        public int Likes { get; set; }
        public int Comments { get; set; }
        public int Reposts { get; set; }
        public int Views { get; set; }
        public bool? IsPinned { get; set; }
        public bool MarkedAsAds { get; set; }
    }
}
