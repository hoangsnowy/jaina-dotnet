using System.Diagnostics;
using Jaina.MultiTenancy;

namespace Jaina.ServiceDiscovery;

/// <summary>
/// HttpClient delegating handler that propagates the resolved tenant id (from
/// <see cref="ITenantContext"/>) and correlation id (from <c>Activity.Current</c>) to every
/// outbound request. Service-to-service calls inherit tenant scope without callers having
/// to remember to forward the headers.
/// </summary>
public sealed class TenantPropagationHandler : DelegatingHandler
{
    private readonly ITenantContext _tenants;

    public TenantPropagationHandler(ITenantContext tenants)
    {
        _tenants = tenants;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_tenants.HasTenant && !request.Headers.Contains("X-Tenant"))
            request.Headers.TryAddWithoutValidation("X-Tenant", _tenants.Current!.TenantId);

        // W3C trace context already auto-propagated by HttpClient. Carry the legacy
        // correlation-id header too so older services that don't read traceparent still see it.
        if (Activity.Current?.Id is { } activityId && !request.Headers.Contains("X-Correlation-Id"))
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", activityId);

        return base.SendAsync(request, ct);
    }
}
