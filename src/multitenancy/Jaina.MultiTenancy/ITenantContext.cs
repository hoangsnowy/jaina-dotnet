using Microsoft.AspNetCore.Http;

namespace Jaina.MultiTenancy;

/// <summary>
/// Per-request snapshot of the resolved tenant. Inject as a scoped dependency in handlers,
/// repositories, and services that need to partition data or behavior by tenant.
/// </summary>
public interface ITenantContext
{
    /// <summary>Currently resolved tenant; null if no resolver matched (anonymous traffic).</summary>
    TenantInfo? Current { get; }

    /// <summary>Convenience — true if a tenant was resolved.</summary>
    bool HasTenant { get; }

    /// <summary>Set the current tenant. Called by the resolution middleware; do not call from app code.</summary>
    void Set(TenantInfo tenant);
}

internal sealed class TenantContext : ITenantContext
{
    public TenantInfo? Current { get; private set; }
    public bool HasTenant => Current is not null;
    public void Set(TenantInfo tenant) => Current = tenant;
}

/// <summary>
/// Strategy for extracting a tenant from an incoming request. Multiple resolvers can be
/// chained via <see cref="JainaMultiTenancyBuilder"/>; the first non-null result wins.
/// </summary>
public interface ITenantResolver
{
    /// <summary>Extract a tenant from the request, or return null to defer to the next resolver.</summary>
    TenantInfo? Resolve(HttpContext context);
}
