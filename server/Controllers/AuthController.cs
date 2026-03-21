using ApiServer.Data;
using ApiServer.Models;
using ApiServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IOAuthClaimsExtractor _claimsExtractor;
    private readonly IJwtService _jwtService;

    public AuthController(AppDbContext db, IOAuthClaimsExtractor claimsExtractor, IJwtService jwtService)
    {
        _db = db;
        _claimsExtractor = claimsExtractor;
        _jwtService = jwtService;
    }

    [HttpGet("login/{provider}")]
    public IActionResult Login(string provider)
    {
        var scheme = _claimsExtractor.SchemeFor(provider);
        if (scheme is null) return BadRequest($"Unknown provider: {provider}");

        var redirectUri = Url.Action(nameof(Callback), new { provider })!;
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, scheme);
    }

    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback(string provider)
    {
        var scheme = _claimsExtractor.SchemeFor(provider);
        if (scheme is null) return BadRequest($"Unknown provider: {provider}");

        var result = await HttpContext.AuthenticateAsync(scheme);
        var userInfo = _claimsExtractor.Extract(provider, result);
        if (userInfo is null) return Unauthorized();

        var player = await _db.Players.FirstOrDefaultAsync(p =>
            p.OAuthProvider == userInfo.Provider && p.OAuthId == userInfo.OAuthId);

        if (player is null)
        {
            player = new Player
            {
                Id = Guid.NewGuid(),
                OAuthProvider = userInfo.Provider,
                OAuthId = userInfo.OAuthId,
                DisplayName = "PENDING",
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
            };
            _db.Players.Add(player);
        }
        else
        {
            player.LastActiveAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        var (token, expires) = _jwtService.GenerateToken(player);
        return Ok(new { token, expires });
    }
}
