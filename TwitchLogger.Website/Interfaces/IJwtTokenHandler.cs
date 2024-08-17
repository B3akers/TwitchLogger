using System.Security.Claims;

namespace TwitchLogger.Website.Interfaces
{
    public interface IJwtTokenHandler
    {
        public string GenerateToken(ClaimsIdentity claims, DateTime? expires);
        public ClaimsPrincipal ValidateToken(string token);
    }
}
