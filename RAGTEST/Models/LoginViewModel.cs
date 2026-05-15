using System.ComponentModel.DataAnnotations;

namespace RAGTEST.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Введите логин или email.")]
        public string LoginOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите пароль.")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
