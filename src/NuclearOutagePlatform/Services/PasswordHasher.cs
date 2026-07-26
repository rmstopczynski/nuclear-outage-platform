using System;
using System.Security.Cryptography;

namespace MVC_EF_Start_8.Services
{
    /// <summary>
    /// Hand-rolled PBKDF2 password hashing via System.Security.Cryptography
    /// -- deliberately NOT Microsoft.AspNetCore.Identity's PasswordHasher&lt;T&gt;.
    /// Pulling in that package means pulling in (or at least being uncertain
    /// about) the full Identity membership system, for a project that only
    /// needs "hash a password, verify a password." This stays entirely in
    /// the base class library: zero extra package risk, and a good way to
    /// show real, from-scratch understanding of what Identity does under
    /// the hood rather than just calling it.
    ///
    /// Format stored in User.PasswordHash: "{iterations}.{salt}.{hash}",
    /// with salt and hash as base64. Storing the iteration count alongside
    /// the hash means it can be increased later (as hardware gets faster)
    /// without invalidating passwords hashed under the old count.
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSizeBytes = 16;
        private const int HashSizeBytes = 32; // 256 bits, matches SHA256
        private const int DefaultIterations = 100_000;

        public static string Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                DefaultIterations,
                HashAlgorithmName.SHA256,
                HashSizeBytes);

            return $"{DefaultIterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public static bool Verify(string password, string storedHash)
        {
            var parts = storedHash.Split('.', 3);
            if (parts.Length != 3)
                return false;

            if (!int.TryParse(parts[0], out int iterations))
                return false;

            byte[] salt;
            byte[] expectedHash;
            try
            {
                salt = Convert.FromBase64String(parts[1]);
                expectedHash = Convert.FromBase64String(parts[2]);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            // Timing-safe comparison -- a naive == or SequenceEqual would
            // leak how many leading bytes matched via response timing.
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
    }
}
