using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KnotShoreControlPlane.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KnotShoreControlPlane.Features.Auth;

public interface ITokenService
{
    TokenResult CreateToken(ApplicationUser user);
}

public record TokenResult(string Token, DateTime ExpiresAtUtc);

public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public TokenResult CreateToken(ApplicationUser user)
    {
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
        };

        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        if (!string.IsNullOrEmpty(user.UserName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new TokenResult(encoded, expiresAt);
    }
}
