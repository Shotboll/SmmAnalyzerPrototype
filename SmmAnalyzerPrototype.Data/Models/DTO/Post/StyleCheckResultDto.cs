using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class StyleCheckResultDto
    {
        public string Assessment { get; set; } = string.Empty; // соответствует / частично соответствует / не соответствует
        public string Summary { get; set; } = string.Empty;
        public List<string> Strengths { get; set; } = new();
        public List<string> Issues { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }
}
