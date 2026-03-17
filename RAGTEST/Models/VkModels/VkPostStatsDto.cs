namespace RAGTEST.Models.VkModels
{
    public class VkPostStatsDto
    {
        public long PostId { get; set; }
        public int Likes { get; set; }
        public int Comments { get; set; }
        public int Reposts { get; set; }
        public int Views { get; set; }
    }
}
