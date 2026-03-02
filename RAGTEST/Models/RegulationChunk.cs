using Pgvector;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RAGTEST.Models
{
    [Table("regulation_chunks")]

    public class RegulationChunk
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Column("community_id")]
        public Guid CommunityId { get; set; }
        [Column("regulation_id")]
        public Guid RegulationId { get; set; }

        [Column("chunk_text")]
        public string ChunkText { get; set; } = string.Empty;

        [Column("chunk_index")]
        public int ChunkIndex { get; set; }

        [Column("embedding", TypeName = "vector(1024)")]
        public Vector? Embedding { get; set; } 

        [Column("metadata")]
        public string? MetadataJson { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
