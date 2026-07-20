using System;
using System.ComponentModel.DataAnnotations;

namespace MVC_EF_Start_8.Models
{
    /// <summary>
    /// The real, persisted domain entity for a nuclear outage record.
    ///
    /// This replaces the original OutageRecord, which had no primary key at
    /// all -- the app used "facility" (a string) as an implicit unique key,
    /// which breaks the moment a facility has more than one day's worth of
    /// data (which is every facility, since this is daily time-series data).
    /// Update/Delete previously matched the FIRST record with a given
    /// facility name, silently editing/deleting the wrong day's record.
    ///
    /// Fields are also now properly typed (DateOnly, decimal?) instead of
    /// everything being a string -- the original stored capacity/outage/
    /// percentOutage as raw strings and re-parsed them with
    /// double.TryParse/DateTime.TryParse on every read, every request.
    /// </summary>
    public class OutageRecord
    {
        public int Id { get; set; }

        [Required]
        public DateOnly Period { get; set; }

        [Required]
        [MaxLength(50)]
        public string Facility { get; set; } = string.Empty;

        [MaxLength(200)]
        public string FacilityName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Generator { get; set; }

        public decimal? Capacity { get; set; }

        public decimal? Outage { get; set; }

        public decimal? PercentOutage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
