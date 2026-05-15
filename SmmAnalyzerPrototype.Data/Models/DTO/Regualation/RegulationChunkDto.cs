using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Regualtion
{
    public class RegulationChunkDto
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Index { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
