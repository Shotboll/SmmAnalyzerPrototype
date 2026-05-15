using System.ComponentModel.DataAnnotations;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Auth
{
    public class RegisterRequest
    {
        [Required]
        [MaxLength(255)]
        public string Login { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(255)]
        public string? Email { get; set; }
    }
}