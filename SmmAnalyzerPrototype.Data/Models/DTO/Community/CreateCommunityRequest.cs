using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Community
{
    public class CreateCommunityRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? TargetAudience { get; set; }
        public string? StyleProfile { get; set; }
    }
}
