using Microsoft.Extensions.DependencyInjection;

namespace Jaina.MultiTenancy;

/// <summary>
/// Fluent builder returned from <c>services.AddJainaMultiTenancy()</c>. Add resolver
/// strategies in priority order — first non-null wins at runtime.
/// </summary>
public sealed class JainaMultiTenancyBuilder
{
    private readonly IServiceCollection _services;
    internal JainaMultiTenancyBuilder(IServiceCollection services) => _services = services;

    public JainaMultiTenancyBuilder FromHeader(string headerName = "X-Tenant")
    {
        _services.AddSingleton<ITenantResolver>(_ => new HeaderTenantResolver(headerName));
        return this;
    }

    public JainaMultiTenancyBuilder FromClaim(string claimType = "tid")
    {
        _services.AddSingleton<ITenantResolver>(_ => new ClaimTenantResolver(claimType));
        return this;
    }

    /// <summary>
    /// Match host header against a regex. The first capture group becomes the tenant id.
    /// Example: <c>FromHost(@"^([^.]+)\.example\.com$")</c> for tenant subdomains.
    /// </summary>
    public JainaMultiTenancyBuilder FromHost(string pattern)
    {
        _services.AddSingleton<ITenantResolver>(_ => new HostTenantResolver(pattern));
        return this;
    }

    public JainaMultiTenancyBuilder FromRoute(string routeKey = "tenant")
    {
        _services.AddSingleton<ITenantResolver>(_ => new RouteTenantResolver(routeKey));
        return this;
    }
}
