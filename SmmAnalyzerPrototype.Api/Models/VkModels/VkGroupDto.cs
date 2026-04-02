namespace SmmAnalyzerPrototype.Api.Models.VkModels
{
    public class VkGroupDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string ScreenName { get; set; }
        public int MembersCount { get; set; }
        public string Description { get; set; }
        public string Activity { get; set; }
        public bool Verified { get; set; }
    }
}
