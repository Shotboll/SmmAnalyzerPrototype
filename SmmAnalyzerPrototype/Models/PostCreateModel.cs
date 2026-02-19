using System.ComponentModel.DataAnnotations;

namespace SmmAnalyzerPrototype.Models
{
    public class PostCreateModel
    {
        [Required(ErrorMessage = "Выберите сообщество")]
        public Guid CommunityId { get; set; }

        [Required(ErrorMessage = "Текст не указан")]
        [StringLength(5000, ErrorMessage = "Превышена максимальная длина текста")]
        public string Text { get; set; } = string.Empty;
    }
}
