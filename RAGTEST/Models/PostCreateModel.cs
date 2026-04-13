using SmmAnalyzerPrototype.Data.Models.DTO.Community;
using System.ComponentModel.DataAnnotations;

namespace RAGTEST.Models
{
    public class PostCreateModel
    {
        public Guid PostId { get; set; }

        [Required(ErrorMessage = "Введите текст поста")]
        [Display(Name = "Текст поста")]
        public string Text { get; set; } = string.Empty;

        [Required(ErrorMessage = "Выберите сообщество")]
        [Display(Name = "Сообщество")]
        public Guid CommunityId { get; set; }

        public string? CommunityName { get; set; }

        public List<CommunityDto>? Communities { get; set; }
    }
}