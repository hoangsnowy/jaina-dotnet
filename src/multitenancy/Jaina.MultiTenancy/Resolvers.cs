using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Jaina.MultiTenancy;

/// <summary>Reads tenant from a configured request header (default <c>X-Tenant</c>).</summary>
public sealed class HeaderTenantResolver : ITenantResolver
{
    private readonly string _headerName;
    public HeaderTenantResolver(string headerName) => _headerName = headerName;

    public TenantInfo? Resolve(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(_headerName, out var values)) return null;
        var id = values.FirstOrDefault();
        return string.IsNullOrWhiteSpace(id) ? null : new TenantInfo { TenantId = id! };
    }
}

/// <summary>Reads tenant from a configured authentication claim (default <c>tid</c>).</summary>
public sealed class ClaimTenantResolver : ITenantResolver
{
    private readonly string _claimType;
    public ClaimTenantResolver(string claimType) => _claimType = claimType;

    public TenantInfo? Resolve(HttpContext context)
    {
        var claim = context.User?.FindFirst(_claimType);
        if (claim is null || string.IsNullOrWhiteSpace(claim.Value)) return null;
        return new TenantInfo { TenantId = claim.Value };
    }
}

/// <summary>
/// Extracts tenant id from the host header — useful for <c>tenant.example.com</c> style
/// per-tenant subdomains. Configured with a regex; the first capture group is the tenant id.
/// </summary>
public sealed class HostTenantResolver : ITenantResolver
{
    private readonly System.Text.RegularExpressions.Regex _pattern;
    public HostTenantResolver(string pattern) => _pattern = new(pattern, System.Text.RegularExpressions.RegexOptions.Compiled);

    public TenantInfo? Resolve(HttpContext context)
    {
        var host = context.Request.Host.Host;
        var match = _pattern.Match(host);
        if (!match.Success || match.Groups.Count < 2) return null;
        var id = match.Groups[1].Value;
        return string.IsNullOrWhiteSpace(id) ? null : new TenantInfo { TenantId = id };
    }
}

/// <summary>
/// Reads tenant from a configured route segment (default <c>tenant</c>): for routes shaped
/// like <c>/api/{tenant}/orders</c>. Requires routing to have run before middleware reads it.
/// </summary>
public sealed class RouteTenantResolver : ITenantResolver
{
    private readonly string _routeKey;
    public RouteTenantResolver(string routeKey) => _routeKey = routeKey;

    public TenantInfo? Resolve(HttpContext context)
    {
        var routeValues = context.Request.RouteValues;
        if (!routeValues.TryGetValue(_routeKey, out var raw) || raw is null) return null;
        var id = raw.ToString();
        return string.IsNullOrWhiteSpace(id) ? null : new TenantInfo { TenantId = id! };
    }
}

/// <summary>
/// Composes a sequence of resolvers; first non-null result wins. Order matches registration.
/// </summary>
public sealed class CompositeTenantResolver : ITenantResolver
{
    private readonly IReadOnlyList<ITenantResolver> _resolvers;
    public CompositeTenantResolver(IEnumerable<ITenantResolver> resolvers) => _resolvers = resolvers.ToList();

    public TenantInfo? Resolve(HttpContext context)
    {
        foreach (var resolver in _resolvers)
        {
            var t = resolver.Resolve(context);
            if (t is not null) return t;
        }
        return null;
    }
}
