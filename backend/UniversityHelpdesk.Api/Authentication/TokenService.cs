using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Authentication;

public record AccessToken(string Value, TimeSpan Lifetime);

public class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateToken(User user)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(Lifetime),
            Subject = new ClaimsIdentity(
            [
                new Claim(HelpdeskClaimTypes.UserId, user.Id.ToString()),
                new Claim(HelpdeskClaimTypes.Email, user.Email),
                new Claim(HelpdeskClaimTypes.Role, user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(CreateSigningKey(jwt), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), Lifetime);
    }

    public static SymmetricSecurityKey CreateSigningKey(JwtOptions jwt) =>
        new(Encoding.UTF8.GetBytes(jwt.SigningKey));
}
