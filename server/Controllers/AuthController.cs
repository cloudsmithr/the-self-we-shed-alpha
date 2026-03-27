using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ApiServer.Data;
using ApiServer.Models;
using ApiServer.Services;
using ApiServer.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace ApiServer.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string OAuthProviderGoogle = "google";
    private const string JwtCookieName = "gs_auth";
    private const string ExternalScheme = "External";
    private const string RefreshCookieName = "gs_refresh";

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext db, IConfiguration config, ILogger<AuthController> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    // ───────────────────────────── OAuth ─────────────────────────────

    [HttpGet("login/google")]
    public IActionResult LoginGoogle()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleCallback))
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("callback/google")]
    public async Task<IActionResult> GoogleCallback()
    {
        var result = await HttpContext.AuthenticateAsync(ExternalScheme);
        if (!result.Succeeded)
            return Unauthorized();

        try
        {
            var googleId = result.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            //var displayName = result.Principal?.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrWhiteSpace(googleId))
                return Unauthorized();

            //displayName = SanitizeDisplayName(displayName);
            
            // 1) Ensure player exists (and get the canonical row) with a tight race handler.
            var player = await GetOrCreatePlayerCanonicalAsync(OAuthProviderGoogle, googleId);
            
            // 2) Now that player is guaranteed, mint cookies/tokens.
            var now = DateTime.UtcNow;

            // revoke existing refreshtokens
            await _db.RefreshTokens
                .Where(rt => rt.PlayerId == player.Id && rt.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, now));

            var (rawRefresh, refreshEntity) = PrepareRefreshToken(player.Id);
            SetRefreshCookie(rawRefresh, refreshEntity.ExpiresAt);

            var (jwt, jwtExpires) = GenerateJwt(player);
            SetJwtCookie(jwt, jwtExpires);

            await _db.SaveChangesAsync();

            SetNoCacheHeaders();

            var spaOrigin = _config["Authentication:Spa:Origin"]
                            ?? throw new InvalidOperationException("Missing Authentication:Spa:Origin");
            var gamePath = _config["Authentication:Spa:GamePath"] ?? "/game";
            return Redirect($"{spaOrigin.TrimEnd('/')}{gamePath}");
        }
        finally
        {
            await HttpContext.SignOutAsync(ExternalScheme);
        }
    }
    

    private async Task<Player> GetOrCreatePlayerCanonicalAsync(string provider, string oauthId)
    {
        var now = DateTime.UtcNow;

        // Try read first
        var existing = await _db.Players
            .FirstOrDefaultAsync(p => p.OAuthProvider == provider && p.OAuthId == oauthId);

        if (existing is not null)
        {
            existing.LastActiveAt = now;

            // no SaveChanges here; caller will save later
            return existing;
        }

        var displayName = await CallsignGenerator.GenerateUniqueAsync(
            async name => !await _db.Players.AnyAsync(p => p.DisplayName == name)
        );
        
        // Not found: attempt insert
        var created = new Player
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            OAuthProvider = provider,
            OAuthId = oauthId,
            CreatedAt = now,
            LastActiveAt = now
        };

        _db.Players.Add(created);

        try
        {
            // Save only to guarantee the row exists (and to catch 23505)
            await _db.SaveChangesAsync();
            return created;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Another request created it first.
            _db.Entry(created).State = EntityState.Detached;

            var winner = await _db.Players
                .FirstOrDefaultAsync(p => p.OAuthProvider == provider && p.OAuthId == oauthId)
                ?? throw new InvalidOperationException("Player not found after unique constraint violation");

            winner.LastActiveAt = now;
            if (!string.IsNullOrWhiteSpace(displayName) && displayName != winner.DisplayName)
                winner.DisplayName = displayName;

            // no SaveChanges here; caller will save later
            return winner;
        }
    }

    // ───────────────────────────── Refresh ─────────────────────────────

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var incomingToken)
            || string.IsNullOrWhiteSpace(incomingToken))
        {
            _logger.LogDebug("Refresh attempt with missing cookie");
            return Unauthorized();
        }

        var hash = HashToken(incomingToken);

        var storedToken = await _db.RefreshTokens
            .Include(rt => rt.Player)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash);

        if (storedToken is null)
        {
            _logger.LogWarning("Refresh attempt with unknown token hash");
            return Unauthorized();
        }

        // THEFT / REUSE DETECTION
        if (storedToken.RevokedAt is not null)
        {
            _logger.LogWarning("Revoked refresh token used. Family {FamilyId}, Player {PlayerId}",
                storedToken.FamilyId, storedToken.PlayerId);
            ExpireRefreshCookie();
            ExpireJwtCookie();
            return Unauthorized();
        }

        if (storedToken.ConsumedAt is not null)
        {
            _logger.LogWarning(
                "Consumed refresh token replayed — possible theft. Family {FamilyId}, Player {PlayerId}",
                storedToken.FamilyId, storedToken.PlayerId);
            await RevokeFamilyAsync(storedToken.FamilyId);
            ExpireRefreshCookie();
            ExpireJwtCookie();
            return Unauthorized();
        }

        // Expired?
        if (storedToken.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogDebug("Expired refresh token used. Family {FamilyId}, Player {PlayerId}",
                storedToken.FamilyId, storedToken.PlayerId);
            ExpireRefreshCookie();
            ExpireJwtCookie();
            return Unauthorized();
        }

        // ATOMIC CONSUME: only succeed if ConsumedAt is still null
        var now = DateTime.UtcNow;
        var consumed = await _db.RefreshTokens
            .Where(rt => rt.Id == storedToken.Id && rt.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.ConsumedAt, now));

        if (consumed != 1)
        {
            _logger.LogWarning(
                "Concurrent refresh race lost — revoking family. Family {FamilyId}, Player {PlayerId}",
                storedToken.FamilyId, storedToken.PlayerId);
            await RevokeFamilyAsync(storedToken.FamilyId);
            ExpireRefreshCookie();
            ExpireJwtCookie();
            return Unauthorized();
        }

        // Issue new JWT + rotated refresh token (same family)
        var player = storedToken.Player;
        player.LastActiveAt = now;

        var (jwt, jwtExpires) = GenerateJwt(player);
        SetJwtCookie(jwt, jwtExpires);

        var (rawRefresh, refreshEntity) = PrepareRefreshToken(player.Id, storedToken.FamilyId);
        SetRefreshCookie(rawRefresh, refreshEntity.ExpiresAt);

        // Single save: player LastActiveAt + new refresh token
        await _db.SaveChangesAsync();

        SetNoCacheHeaders();

        return NoContent();
    }

    // ───────────────────────────── Logout ─────────────────────────────

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var token)
            && !string.IsNullOrWhiteSpace(token))
        {
            var hash = HashToken(token);
            var storedToken = await _db.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == hash);

            if (storedToken is not null)
            {
                await RevokeFamilyAsync(storedToken.FamilyId);
                await _db.SaveChangesAsync();
            }
        }

        ExpireJwtCookie();
        ExpireRefreshCookie();

        SetNoCacheHeaders();

        return NoContent();
    }

    // ───────────────────────────── Me ─────────────────────────────

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var playerId))
            return Unauthorized();

        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId);
        if (player is null)
            return Unauthorized();

        return Ok(new { player = new { player.Id, player.DisplayName } });
    }

    // ───────────────────────────── JWT ─────────────────────────────

    private (string Token, DateTime Expires) GenerateJwt(Player player)
    {
        var issuer = _config["Authentication:Jwt:Issuer"]
                     ?? throw new InvalidOperationException("Missing Jwt:Issuer");
        var audience = _config["Authentication:Jwt:Audience"]
                       ?? throw new InvalidOperationException("Missing Jwt:Audience");
        var secret = _config["Authentication:Jwt:Secret"]
                     ?? throw new InvalidOperationException("Missing Jwt:Secret");

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Authentication:Jwt:Secret must be at least 32 bytes.");

        var key = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

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

    // ───────────────────────────── Refresh Tokens ─────────────────────────────

    /// <summary>
    /// Prepares a new refresh token entity and adds it to the context (without saving).
    /// Returns the raw token for the cookie and the entity for reading ExpiresAt.
    /// Caller is responsible for calling SaveChangesAsync.
    /// </summary>
    private (string RawToken, RefreshToken Entity) PrepareRefreshToken(Guid playerId, Guid? familyId = null)
    {
        var expiryStr = _config["Authentication:Jwt:RefreshExpiryInDays"];
        var expiryInDays = int.TryParse(expiryStr, out var d) ? d : 7;

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = HashToken(rawToken),
            PlayerId = playerId,
            FamilyId = familyId ?? Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryInDays),
        };

        _db.RefreshTokens.Add(entity);

        return (rawToken, entity);
    }

    private string HashToken(string rawToken)
    {
        var pepper = _config["Authentication:Jwt:TokenPepper"]
                     ?? throw new InvalidOperationException("Missing Authentication:Jwt:TokenPepper");

        var key = Encoding.UTF8.GetBytes(pepper);
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(hash);
    }

    private async Task RevokeFamilyAsync(Guid familyId)
    {
        var now = DateTime.UtcNow;
        await _db.RefreshTokens
            .Where(rt => rt.FamilyId == familyId && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, now));
    }

    // ───────────────────────────── Cookies ─────────────────────────────

    private CookieOptions BuildJwtCookieOptions(DateTimeOffset? expires = null)
    {
        var cookieDomain = _config["Authentication:Jwt:CookieDomain"];
        var domain = string.IsNullOrWhiteSpace(cookieDomain) ? null : cookieDomain;

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Domain = domain,
            Expires = expires
        };
    }

    private void SetJwtCookie(string jwt, DateTime expires)
    {
        Response.Cookies.Append(JwtCookieName, jwt, BuildJwtCookieOptions(expires));
    }

    private void ExpireJwtCookie()
    {
        Response.Cookies.Append(JwtCookieName, "", BuildJwtCookieOptions(DateTimeOffset.UnixEpoch));
        Response.Cookies.Delete(JwtCookieName, BuildJwtCookieOptions(DateTimeOffset.UnixEpoch));
    }

    private void SetRefreshCookie(string token, DateTime expires)
    {
        var cookieDomain = _config["Authentication:Jwt:CookieDomain"];
        var domain = string.IsNullOrWhiteSpace(cookieDomain) ? null : cookieDomain;

        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Domain = domain,
            Expires = expires,
        });
    }

    private void ExpireRefreshCookie()
    {
        var cookieDomain = _config["Authentication:Jwt:CookieDomain"];
        var domain = string.IsNullOrWhiteSpace(cookieDomain) ? null : cookieDomain;

        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Domain = domain,
        });
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    private void SetNoCacheHeaders()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    private static string SanitizeDisplayName(string? name)
    {
        name = (name ?? "Unknown").Trim();
        if (name.Length == 0) name = "Unknown";
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());
        if (name.Length > 40) name = name[..40];
        return name.Length == 0 ? "Unknown" : name;
    }
}
