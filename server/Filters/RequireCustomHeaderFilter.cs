using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApiServer.Filters;

public class RequireCustomHeaderFilter : IAuthorizationFilter
{
    private const string HeaderName = "X-Requested-With";
    private const string ExpectedValue = "XMLHttpRequest";

    private static readonly HashSet<string> ProtectedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "DELETE", "PATCH"
    };

    private readonly string _spaOrigin;

    public RequireCustomHeaderFilter(IConfiguration config)
    {
        _spaOrigin = (config["Authentication:Spa:Origin"]
                      ?? throw new InvalidOperationException("Missing Authentication:Spa:Origin"))
            .TrimEnd('/');
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var req = context.HttpContext.Request;

        if (!ProtectedMethods.Contains(req.Method))
            return;

        // If Origin is present, enforce exact match
        if (req.Headers.TryGetValue("Origin", out var origin) &&
            !string.IsNullOrWhiteSpace(origin) &&
            !string.Equals(origin.ToString().TrimEnd('/'), _spaOrigin, StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        // Require a non-simple header to block cross-site form posts
        if (!req.Headers.TryGetValue(HeaderName, out var value) ||
            value.Count == 0 ||
            !string.Equals(value.ToString(), ExpectedValue, StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
    }
}