using System;
using System.ComponentModel.DataAnnotations;

namespace MVC_EF_Start_8.Models.Api
{
    /// <summary>
    /// What a client is allowed to submit when creating/updating an outage
    /// record via the API. Deliberately excludes Id/CreatedAt/UpdatedAt --
    /// letting a client set those directly is a classic over-posting bug
    /// (binding the request straight to the EF entity would let a client
    /// set CreatedAt to whatever it wants, or even try to set Id).
    /// </summary>
    public class OutageWriteRequest
    {
        [Required]
        public DateOnly Period { get; set; }

        [Required]
        [MaxLength(50)]
        public string Facility { get; set; } = string.Empty;

        [MaxLength(200)]
        public string FacilityName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Generator { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Capacity cannot be negative.")]
        public decimal? Capacity { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Outage cannot be negative.")]
        public decimal? Outage { get; set; }

        [Range(0, 100, ErrorMessage = "PercentOutage must be between 0 and 100.")]
        public decimal? PercentOutage { get; set; }
    }
}
