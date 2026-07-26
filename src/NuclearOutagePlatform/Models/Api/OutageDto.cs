namespace MVC_EF_Start_8.Models.Api
{
    /// <summary>
    /// JSON response shape for the REST API layer (Step 4). Kept separate
    /// from OutageRecord for the same reason EiaOutageDto is kept separate
    /// from it on the ingestion side: the wire format and the persisted
    /// entity are different concerns, and shouldn't have to change
    /// together. Also avoids ever serializing internal-only fields.
    /// </summary>
    public class OutageDto
    {
        public int Id { get; set; }
        public string Period { get; set; } = string.Empty; // yyyy-MM-dd
        public string Facility { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public string? Generator { get; set; }
        public decimal? Capacity { get; set; }
        public decimal? Outage { get; set; }
        public decimal? PercentOutage { get; set; }
        public string CreatedAt { get; set; } = string.Empty; // ISO 8601 UTC
        public string UpdatedAt { get; set; } = string.Empty; // ISO 8601 UTC

        public static OutageDto FromEntity(OutageRecord record) => new()
        {
            Id = record.Id,
            Period = record.Period.ToString("yyyy-MM-dd"),
            Facility = record.Facility,
            FacilityName = record.FacilityName,
            Generator = record.Generator,
            Capacity = record.Capacity,
            Outage = record.Outage,
            PercentOutage = record.PercentOutage,
            CreatedAt = record.CreatedAt.ToString("o"),
            UpdatedAt = record.UpdatedAt.ToString("o"),
        };
    }
}
