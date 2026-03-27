using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApiServer.Models;
using Microsoft.IdentityModel.Tokens;

namespace ApiServer.Services;

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(Player player);
}

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(Player player)
    {
        string issuer = _config["Authentication:Jwt:Issuer"]
            ?? throw new InvalidOperationException("Missing Jwt:Issuer");
        string audience = _config["Authentication:Jwt:Audience"]
            ?? throw new InvalidOperationException("Missing Jwt:Audience");
        string secret = _config["Authentication:Jwt:Secret"]
            ?? throw new InvalidOperationException("Missing Jwt:Secret");

        string expiryStr = _config["Authentication:Jwt:ExpiryInMinutes"]
            ?? throw new InvalidOperationException("Missing Jwt:ExpiryInMinutes");
        
        int expiryInMinutes = int.TryParse(expiryStr, out var m) ? m : 15;
        
        DateTime now = DateTime.UtcNow;
        DateTime expiresAt = now.AddMinutes(expiryInMinutes);

        
        byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Authentication:Jwt:Secret must be at least 32 bytes.");

        SymmetricSecurityKey key = new SymmetricSecurityKey(keyBytes);
        SigningCredentials credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);


        List<Claim> claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, player.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
        };



        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
