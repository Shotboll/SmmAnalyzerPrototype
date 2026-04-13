using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmmAnalyzerPrototype.Data.Models
{
    [Table("prohibited_topic_matches")]
    public class ProhibitedTopicMatch
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid AnalysisResultId { get; set; }

        [ForeignKey(nameof(AnalysisResultId))]
        public virtual AnalysisResult AnalysisResult { get; set; } = null!;

        [MaxLength(255)]
        public string Topic { get; set; } = string.Empty;

        [Column(TypeName = "text")]
        public string? Evidence { get; set; }

        [MaxLength(255)]
        [Column("regulation_ref")]
        public string? RegulationRef { get; set; }

        [Column(TypeName = "text")]
        public string? Explanation { get; set; }
    }
}