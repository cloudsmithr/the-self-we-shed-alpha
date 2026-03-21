using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

namespace ApiServer.Services;

public interface IOAuthClaimsExtractor
{
    string? SchemeFor(string provider);
    OAuthUserInfo? Extract(string provider, AuthenticateResult result);
}

public class OAuthClaimsExtractor : IOAuthClaimsExtractor
{
    private static readonly Dictionary<string, string> _schemes = new()
    {
        [OAuthProviders.Google] = GoogleDefaults.AuthenticationScheme,
    };

    private static readonly Dictionary<string, Func<ClaimsPrincipal, OAuthUserInfo?>> _extractors = new()
    {
        [OAuthProviders.Google] = principal =>
        {
            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id is null) return null;
            return new OAuthUserInfo(OAuthProviders.Google, id);
        },
    };

    public string? SchemeFor(string provider) =>
        _schemes.TryGetValue(provider.ToLower(), out var scheme) ? scheme : null;

    public OAuthUserInfo? Extract(string provider, AuthenticateResult result)
    {
        if (!result.Succeeded || result.Principal is null) return null;
        if (!_extractors.TryGetValue(provider.ToLower(), out var extractor)) return null;
        return extractor(result.Principal);
    }
}
