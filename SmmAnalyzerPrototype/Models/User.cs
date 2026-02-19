namespace SmmAnalyzerPrototype.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public UserRole Role { get; set; }
    }
    public enum UserRole
    {
        ContentManager,
        Administrator
    }
}
