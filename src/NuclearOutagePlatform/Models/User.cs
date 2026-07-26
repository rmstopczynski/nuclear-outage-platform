using System;
using System.ComponentModel.DataAnnotations;

namespace MVC_EF_Start_8.Models
{
    /// <summary>
    /// A registered user account. Step 3 (see README) adds real auth on top
    /// of the existing outage-tracking features: JWT stored in an HttpOnly
    /// cookie, PBKDF2 password hashing (no ASP.NET Core Identity package --
    /// see PasswordHasher.cs for the reasoning).
    /// </summary>
    public class User
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Stored as "{iterations}.{salt}.{hash}" (all base64 except the
        /// iteration count) so the iteration count can be bumped later
        /// without invalidating existing hashes. See PasswordHasher.cs.
        /// </summary>
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
