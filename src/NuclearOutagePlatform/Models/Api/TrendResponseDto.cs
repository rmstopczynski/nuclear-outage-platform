using System.Collections.Generic;

namespace MVC_EF_Start_8.Models.Api
{
    public class TrendPointDto
    {
        public string Period { get; set; } = string.Empty; // yyyy-MM-dd
        public decimal TotalOutage { get; set; }
        public int RecordCount { get; set; }
    }

    public class TrendResponseDto
    {
        public string? Facility { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public List<TrendPointDto> Points { get; set; } = new();
    }
}
