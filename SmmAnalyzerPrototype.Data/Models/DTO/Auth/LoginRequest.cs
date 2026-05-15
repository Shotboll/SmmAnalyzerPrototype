using System.ComponentModel.DataAnnotations;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Auth
{
    public class LoginRequest
    {
        [Required]
        public string LoginOrEmail { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}