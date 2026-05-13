using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmmAnalyzerPrototype.Data.Models.DTO.Post
{
    public class ForecastRequestDto
    {
        public Guid CommunityId { get; set; }

        public string NewPostText { get; set; } = string.Empty;

        public int HistoryDays { get; set; } = 30;
    }
}
