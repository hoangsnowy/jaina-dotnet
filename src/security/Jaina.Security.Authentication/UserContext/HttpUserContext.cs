using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Jaina.Security.Authentication.UserContext;

/// <summary>
/// <see cref="IUserContext"/> implementation backed by <see cref="IHttpContextAccessor"/>.
/// Reads from the current request's <c>HttpContext.User</c>. Scope handling expects the
/// standard <c>scope</c> claim with space-separated values (RFC 8693).
/// </summary>
public sealed class HttpUserContext : IUserContext
{
    private readonly IHttpContextAccessor _accessor;
    public HttpUserContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string? UserId => Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? Principal?.FindFirst("sub")?.Value;

    public string? UserName => Principal?.Identity?.Name
                            ?? Principal?.FindFirst("preferred_username")?.Value;

    public bool HasScope(string scope)
    {
        var raw = Principal?.FindFirst("scope")?.Value
               ?? Principal?.FindFirst("scp")?.Value;
        if (string.IsNullOrEmpty(raw)) return false;
        return raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Contains(scope, StringComparer.Ordinal);
    }

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public string? GetClaim(string claimType) => Principal?.FindFirst(claimType)?.Value;
}
