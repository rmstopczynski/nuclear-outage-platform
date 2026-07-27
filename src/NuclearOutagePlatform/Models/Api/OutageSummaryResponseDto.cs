namespace MVC_EF_Start_8.Models.Api
{
    public class OutageSummaryResponseDto
    {
        public string Summary { get; set; } = string.Empty;
        public string? Facility { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public int RecordCount { get; set; }
        public string GeneratedAt { get; set; } = string.Empty; // ISO 8601 UTC
    }
}
