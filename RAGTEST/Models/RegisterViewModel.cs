using System.ComponentModel.DataAnnotations;

namespace RAGTEST.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Введите логин.")]
        [MaxLength(255)]
        public string Login { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите пароль.")]
        [MinLength(6, ErrorMessage = "Пароль должен содержать не менее 6 символов.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Подтвердите пароль.")]
        [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Введите корректный адрес электронной почты.")]
        public string? Email { get; set; }
    }
}
