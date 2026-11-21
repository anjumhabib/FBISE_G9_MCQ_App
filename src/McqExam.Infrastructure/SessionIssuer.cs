using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using McqExam.Application;
using McqExam.Domain;
using Microsoft.IdentityModel.Tokens;

namespace McqExam.Infrastructure;

public sealed record JwtOptions(string Issuer, string Audience, SymmetricSecurityKey Key);

public sealed class SessionIssuer(JwtOptions options, TimeProvider clock) : ISessionIssuer
{
    public AuthSession Issue(User user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddHours(8);
        var jwt = new JwtSecurityToken(options.Issuer, options.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))],
            now, expires, new SigningCredentials(options.Key, SecurityAlgorithms.HmacSha256));
        return new AuthSession(new JwtSecurityTokenHandler().WriteToken(jwt),
            new Profile(user.Id, user.FullName, user.Email), expires);
    }
}
