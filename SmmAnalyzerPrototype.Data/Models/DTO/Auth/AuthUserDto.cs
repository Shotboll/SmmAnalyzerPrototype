namespace SmmAnalyzerPrototype.Data.Models.DTO.Auth
{
    public class AuthUserDto
    {
        public Guid Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string? Email { get; set; }
    }
}