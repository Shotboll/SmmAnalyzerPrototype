using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class GrammarExplanationDto
    {
        public int Index { get; set; }
        public string Explanation { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
    }
}
