using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class GrammarFilterResult
    {
        public List<GrammarErrorDto> AcceptedErrors { get; set; } = new();
        public List<GrammarErrorDto> SuspiciousErrors { get; set; } = new();
        public List<GrammarErrorDto> RejectedErrors { get; set; } = new();
    }
}