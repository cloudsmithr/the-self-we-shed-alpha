using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApiServer.Models;
using Microsoft.IdentityModel.Tokens;

namespace ApiServer.Services;

public interface IJwtService
{
    (string Token, DateTime Expires) GenerateToken(Player player);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public (string Token, DateTime Expires) GenerateToken(Player player)
    {
        var secret = _config["Authentication:Jwt:Secret"] ?? throw new InvalidOperationException("Missing Authentication:Jwt:Secret");
        var issuer = _config["Authentication:Jwt:Issuer"] ?? throw new InvalidOperationException("Missing Authentication:Jwt:Issuer");
        var audience = _config["Authentication:Jwt:Audience"] ?? throw new InvalidOperationException("Missing Authentication:Jwt:Audience");

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Authentication:Jwt:Secret must be at least 32 bytes.");

        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, player.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
        };

        var expiryStr = _config["Authentication:Jwt:ExpiryInMinutes"];
        var expiryInMinutes = int.TryParse(expiryStr, out var m) ? m : 15;

        var expires = now.AddMinutes(expiryInMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
