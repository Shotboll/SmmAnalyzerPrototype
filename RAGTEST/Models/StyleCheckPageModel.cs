using SmmAnalyzerPrototype.Data.Models.DTO.Community;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;
using System.ComponentModel.DataAnnotations;

namespace RAGTEST.Models
{
    public class StyleCheckPageModel
    {
        [Required(ErrorMessage = "Выберите сообщество")]
        public Guid? CommunityId { get; set; }

        [Required(ErrorMessage = "Введите текст поста")]
        [StringLength(5000, ErrorMessage = "Максимальная длина текста — 5000 символов")]
        [MinLength(20, ErrorMessage = "Текст слишком короткий для анализа")]
        public string Text { get; set; } = string.Empty;

        public List<CommunityDto> Communities { get; set; } = new();

        public StyleCheckResultDto? Result { get; set; }

        public string? ErrorMessage { get; set; }

        public Guid PostId { get; set; }
        public string? CommunityName { get; set; }
    }
}
