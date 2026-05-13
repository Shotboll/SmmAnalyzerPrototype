using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Community
{
    public class SamplePostDto
    {
        public string ShortText { get; set; } = string.Empty;
        public int Likes { get; set; }
        public int Comments { get; set; }
    }
}
