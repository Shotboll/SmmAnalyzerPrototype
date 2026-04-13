using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmmAnalyzerPrototype.Data.Models
{
    [Table("grammar_errors")]
    public class GrammarError
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid AnalysisResultId { get; set; }

        [ForeignKey(nameof(AnalysisResultId))]
        public virtual AnalysisResult AnalysisResult { get; set; } = null!;

        [Required]
        [MaxLength(500)]
        public string Fragment { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Suggestion { get; set; }

        [MaxLength(100)]
        [Column("error_type")]
        public string? ErrorType { get; set; }

        public int Position { get; set; }

        [Column(TypeName = "text")]
        public string? Message { get; set; }

        [Column("is_suspicious")]
        public bool IsSuspicious { get; set; }
    }
}