using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ProfileBook.API.Data;

namespace ProfileBook.API.Helpers
{
    public static class AuthHelper
    {
        public static int? GetCurrentUserId(ClaimsPrincipal? user, ProfileBookDbContext context, string? bearerToken = null)
        {
            var claimCandidates = new[]
            {
                user?.FindFirstValue(ClaimTypes.NameIdentifier),
                user?.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"),
                user?.FindFirstValue("nameid")
            };

            foreach (var candidate in claimCandidates)
            {
                if (int.TryParse(candidate, out var userId))
                    return userId;
            }

            var email = user?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? user?.FindFirstValue("sub")
                ?? user?.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(bearerToken))
            {
                var tokenValue = bearerToken.Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                if (!string.IsNullOrWhiteSpace(tokenValue))
                {
                    var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenValue);
                    foreach (var candidate in token.Claims.Where(c =>
                                 c.Type == ClaimTypes.NameIdentifier ||
                                 c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" ||
                                 c.Type == "nameid"))
                    {
                        if (int.TryParse(candidate.Value, out var tokenUserId))
                            return tokenUserId;
                    }

                    email = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")?.Value;
                }
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var normalizedEmail = IdentityNormalizer.NormalizeEmail(email);
                return context.Users
                    .Where(u => u.Email.Trim().ToLower() == normalizedEmail)
                    .Select(u => (int?)u.UserId)
                    .FirstOrDefault();
            }

            return null;
        }
    }
}
