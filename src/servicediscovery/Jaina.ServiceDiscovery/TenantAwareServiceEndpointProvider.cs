using Jaina.MultiTenancy;
using Microsoft.Extensions.ServiceDiscovery;

namespace Jaina.ServiceDiscovery;

/// <summary>
/// Wraps the standard service endpoint resolver and tries a tenant-prefixed name first.
/// For a request resolving <c>http://orders</c> with current tenant <c>acme</c>, the chain is:
/// <code>
/// 1. orders.acme  (tenant-specific endpoint)
/// 2. orders       (shared fallback)
/// </code>
/// Useful when select tenants need their own dedicated cluster (regulatory, latency, scale).
/// Configure both names in <c>appsettings.json</c>:
/// <code>
/// "Services": {
///   "orders":      { "http": [ "https://orders.shared.svc" ] },
///   "orders.acme": { "http": [ "https://orders.acme.svc" ] }
/// }
/// </code>
/// </summary>
public sealed class TenantAwareServiceEndpointProvider
{
    private readonly ServiceEndpointResolver _inner;
    private readonly ITenantContext _tenants;

    public TenantAwareServiceEndpointProvider(ServiceEndpointResolver inner, ITenantContext tenants)
    {
        _inner = inner;
        _tenants = tenants;
    }

    /// <summary>
    /// Resolve a logical service name. If the current request has a tenant, tries
    /// <c>{name}.{tenantId}</c> first; falls back to <c>{name}</c> when the tenant-specific
    /// entry is not configured.
    /// </summary>
    public async ValueTask<ServiceEndpointSource> GetEndpointsAsync(string serviceName, CancellationToken ct = default)
    {
        if (_tenants.HasTenant)
        {
            var tenantSpecific = $"{serviceName}.{_tenants.Current!.TenantId}";
            try
            {
                return await _inner.GetEndpointsAsync(tenantSpecific, ct);
            }
            catch (InvalidOperationException)
            {
                // No endpoints configured for tenant-specific name → fall through to shared.
            }
        }

        return await _inner.GetEndpointsAsync(serviceName, ct);
    }
}
