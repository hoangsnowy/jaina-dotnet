using Jaina.MultiTenancy;
using Jaina.Observability.Telemetry;
using Microsoft.Extensions.Localization;

namespace Jaina.Localization;

internal sealed class JainaLocalizer<TResource> : IJainaLocalizer<TResource>
{
    private readonly IStringLocalizer<TResource> _inner;
    private readonly ITenantContext _tenants;

    public JainaLocalizer(IStringLocalizer<TResource> inner, ITenantContext tenants)
    {
        _inner = inner;
        _tenants = tenants;
    }

    public LocalizedString this[string name] => Resolve(name, Array.Empty<object>());

    public LocalizedString this[string name, params object[] arguments] => Resolve(name, arguments);

    private LocalizedString Resolve(string key, object[] args)
    {
        using var span = JainaActivitySource.StartSpan("localization", "lookup");
        span?.SetTag("jaina.localization.key", key);

        var tenantId = _tenants.Current?.TenantId;
        span?.SetTag("jaina.localization.tenant", tenantId ?? "(none)");

        // 1) Tenant-specific override: "{tenant}/{key}"
        if (!string.IsNullOrEmpty(tenantId))
        {
            var tenantKey = $"{tenantId}/{key}";
            var hit = args.Length == 0 ? _inner[tenantKey] : _inner[tenantKey, args];
            if (!hit.ResourceNotFound)
            {
                span?.SetTag("jaina.localization.found", "tenant");
                return hit;
            }
        }

        // 2) Shared fallback: "{key}"
        var shared = args.Length == 0 ? _inner[key] : _inner[key, args];
        span?.SetTag("jaina.localization.found", shared.ResourceNotFound ? "missing" : "shared");
        return shared;
    }
}
