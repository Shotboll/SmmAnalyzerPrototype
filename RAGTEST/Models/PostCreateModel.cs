using SmmAnalyzerPrototype.Data.Models.DTO.Community;
using System.ComponentModel.DataAnnotations;

namespace RAGTEST.Models
{
    public class PostCreateModel
    {
        [Required(ErrorMessage = "Введите текст поста")]
        [Display(Name = "Текст поста")]
        public string Text { get; set; } = string.Empty;

        [Required(ErrorMessage = "Выберите сообщество")]
        [Display(Name = "Сообщество")]
        public Guid CommunityId { get; set; }

        public List<CommunityDto>? Communities { get; set; }
    }
}
