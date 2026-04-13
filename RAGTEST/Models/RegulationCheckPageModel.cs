using Microsoft.AspNetCore.Mvc.Rendering;
using SmmAnalyzerPrototype.Data.Models.DTO.Community;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;

namespace RAGTEST.Models
{
    public class RegulationCheckPageModel
    {
        public Guid CommunityId { get; set; }
        public string Text { get; set; } = string.Empty;

        public List<CommunityDto> Communities { get; set; } = new();

        public bool HasResult { get; set; }
        public bool HasViolations { get; set; }
        public string Comment { get; set; } = string.Empty;
        public List<ViolationDto> Violations { get; set; } = new();

        public Guid PostId { get; set; }
        public string? CommunityName { get; set; }
    }
}
