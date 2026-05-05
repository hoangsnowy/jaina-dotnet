using Microsoft.AspNetCore.Http;

namespace Jaina.MultiTenancy;

/// <summary>
/// Runs registered <see cref="ITenantResolver"/>s and populates the scoped
/// <see cref="ITenantContext"/>. Place after authentication (so claim resolvers see the
/// principal) and before endpoint execution.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IEnumerable<ITenantResolver> _resolvers;

    public TenantResolutionMiddleware(RequestDelegate next, IEnumerable<ITenantResolver> resolvers)
    {
        _next = next;
        _resolvers = resolvers;
    }

    public async Task InvokeAsync(HttpContext ctx, ITenantContext tenantCtx)
    {
        foreach (var resolver in _resolvers)
        {
            var tenant = resolver.Resolve(ctx);
            if (tenant is not null)
            {
                tenantCtx.Set(tenant);
                break;
            }
        }
        await _next(ctx);
    }
}
