using System.ComponentModel.DataAnnotations;

namespace SmmAnalyzerPrototype.Models
{
    public class CommunityCreateModel
    {
        [Required(ErrorMessage = "Укажите название")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите целевую аудиторию")]
        public string TargetAudience { get; set; } = string.Empty;

        [Required(ErrorMessage = "Выберите стиль")]
        public StyleProfile StyleProfile { get; set; }

        public string ContentGoals { get; set; } = string.Empty;
    }
}
