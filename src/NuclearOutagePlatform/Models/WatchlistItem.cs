using System;
using System.ComponentModel.DataAnnotations;

namespace MVC_EF_Start_8.Models
{
    /// <summary>
    /// A single "follow this facility" entry for a user. Facility is stored
    /// as the same short code used on OutageRecord (e.g. "0001") so it can
    /// be joined/filtered against Outages directly; FacilityName is
    /// denormalized alongside it purely for display, so the watchlist page
    /// doesn't need a join just to show a readable name.
    /// </summary>
    public class WatchlistItem
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Facility { get; set; } = string.Empty;

        [MaxLength(200)]
        public string FacilityName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
