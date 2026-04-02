using Pgvector;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmmAnalyzerPrototype.Data.Models
{
    [Table("regulation_chunks")]
    public class RegulationChunk
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid RegulationId { get; set; }

        [ForeignKey("RegulationId")]
        public virtual RegulationDocument Regulation { get; set; } = null!;

        [Column("embedding", TypeName = "vector(1024)")]
        public Vector? Embedding { get; set; }

        [Column("chunk_index")]
        public int ChunkIndex { get; set; }

        [Column("chunk_text", TypeName="text")]
        public string ChunkText { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
