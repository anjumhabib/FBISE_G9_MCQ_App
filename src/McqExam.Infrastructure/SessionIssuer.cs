using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using McqExam.Application;
using McqExam.Domain;
using Microsoft.IdentityModel.Tokens;

namespace McqExam.Infrastructure;

public sealed record JwtOptions(string Issuer, string Audience, SymmetricSecurityKey Key);

public sealed class SessionIssuer(JwtOptions options, TimeProvider clock) : ISessionIssuer
{
    public AuthSession Issue(User user, Guid sessionId, DateTime sessionExpiresAt)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var accessExpiresAt = now.AddHours(8);
        var expires = accessExpiresAt < sessionExpiresAt ? accessExpiresAt : sessionExpiresAt;
        var jwt = new JwtSecurityToken(options.Issuer, options.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Role, user.Role.ToString()), new Claim("sid", sessionId.ToString())],
            now, expires, new SigningCredentials(options.Key, SecurityAlgorithms.HmacSha256));
        return new AuthSession(new JwtSecurityTokenHandler().WriteToken(jwt),
            new Profile(user.Id, user.FullName, user.Email), expires);
    }
}
