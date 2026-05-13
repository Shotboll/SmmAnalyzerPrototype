using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Community
{
    public class CommunityMetricsDto
    {
        public bool HasData { get; set; }
        public int TotalPosts { get; set; }
        public double AvgLikes { get; set; }
        public double AvgComments { get; set; }
        public double AvgReposts { get; set; }
        public double AvgViews { get; set; }
        public double AvgER { get; set; }
        public double PostsPerWeek { get; set; }
        public List<SamplePostDto> TopPosts { get; set; } = new();
    }
}
