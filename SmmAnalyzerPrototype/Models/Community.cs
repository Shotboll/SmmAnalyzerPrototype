namespace SmmAnalyzerPrototype.Models
{
    public class Community
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = string.Empty;
        public StyleProfile StyleProfile { get; set; }
        public string ContentGoals { get; set; } = string.Empty;
    }
    public enum StyleProfile
    {
        Formal,
        Informal,
        Neutral
    }
}
