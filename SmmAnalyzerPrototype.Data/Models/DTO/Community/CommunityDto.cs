using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Community
{
    public class CommunityDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string TargetAudience { get; set; } = string.Empty;

        public string StyleProfile { get; set; } = string.Empty;

        public string? VkInput { get; set; }

        public long? VkGroupId { get; set; }

        public string? VkScreenName { get; set; }

        public string? VkUrl { get; set; }

        public DateTime? VkPostsSyncedAt { get; set; }
    }
}
