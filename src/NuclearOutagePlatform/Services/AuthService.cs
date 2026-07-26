using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MVC_EF_Start_8.DataAccess;
using MVC_EF_Start_8.Models;

namespace MVC_EF_Start_8.Services
{
    /// <summary>
    /// Registration, credential validation, and JWT issuance. Scoped (it
    /// depends on ApplicationDbContext), matching the OutageService pattern
    /// -- controllers depend on this, never on ApplicationDbContext or
    /// PasswordHasher directly.
    /// </summary>
    public class AuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<bool> EmailInUseAsync(string email)
        {
            return await _context.Users.AnyAsync(u => u.Email == email);
        }

        /// <summary>
        /// Returns null on success, or an error message to show the user
        /// (e.g. "email already registered") on failure.
        /// </summary>
        public async Task<(User? user, string? error)> RegisterAsync(string username, string email, string password)
        {
            if (await EmailInUseAsync(email))
                return (null, "An account with that email already exists.");

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = PasswordHasher.Hash(password),
                CreatedAt = DateTime.UtcNow,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return (user, null);
        }

        public async Task<User?> ValidateCredentialsAsync(string email, string password)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return null;

            return PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
        }

        /// <summary>
        /// Issues a signed JWT for the given user. The token is handed to
        /// the caller (AuthController) to store in an HttpOnly cookie --
        /// this method just produces the token string, it doesn't know
        /// about cookies at all.
        /// </summary>
        public string GenerateJwt(User user)
        {
            var jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is not configured.");
            var issuer = _configuration["Jwt:Issuer"] ?? "NuclearOutagePlatform";
            var audience = _configuration["Jwt:Audience"] ?? "NuclearOutagePlatform";
            var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var m) ? m : 1440;

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
