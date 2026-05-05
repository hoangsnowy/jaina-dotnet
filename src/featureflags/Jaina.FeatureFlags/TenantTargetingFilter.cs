using Jaina.MultiTenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;

namespace Jaina.FeatureFlags;

/// <summary>
/// Feature filter that gates a flag on the current tenant id resolved by
/// <see cref="ITenantContext"/>. Configure in <c>appsettings.json</c>:
/// <code>
/// "FeatureManagement": {
///   "BetaPricing": {
///     "EnabledFor": [
///       { "Name": "Jaina.Tenant", "Parameters": {
///           "Tenants": [ "acme", "globex" ],
///           "Percentage": 100
///         }
///       }
///     ]
///   }
/// }
/// </code>
/// <para>
/// <c>Tenants</c>: explicit allow-list. Empty = match any tenant.
/// <c>Percentage</c>: 0–100 deterministic rollout based on a stable hash of the tenant id.
/// Combined: tenant must be in <c>Tenants</c> (if specified) AND fall under the percentage.
/// </para>
/// </summary>
[FilterAlias("Jaina.Tenant")]
public sealed class TenantTargetingFilter : IFeatureFilter
{
    private readonly ITenantContext _tenants;

    public TenantTargetingFilter(ITenantContext tenants)
    {
        _tenants = tenants;
    }

    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        var tenantId = _tenants.Current?.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            return Task.FromResult(false);

        var settings = context.Parameters.Get<TenantFilterSettings>() ?? new TenantFilterSettings();

        // Allow-list check: empty list = wildcard match
        if (settings.Tenants is { Length: > 0 } &&
            !settings.Tenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
            return Task.FromResult(false);

        if (settings.Percentage >= 100) return Task.FromResult(true);
        if (settings.Percentage <= 0)   return Task.FromResult(false);

        // Stable percentage rollout — hash the tenant id so rollout is sticky per tenant
        // (same tenant always lands in the same bucket across requests).
        var bucket = StableBucket(tenantId);
        return Task.FromResult(bucket < settings.Percentage);
    }

    internal static int StableBucket(string key)
    {
        // FNV-1a 32-bit, then modulo 100 — small, deterministic, no allocations
        const uint Prime = 16777619u;
        uint hash = 2166136261u;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(key))
        {
            hash ^= b;
            hash *= Prime;
        }
        return (int)(hash % 100);
    }
}

public sealed class TenantFilterSettings
{
    /// <summary>Optional allow-list of tenant ids. Empty = match any tenant.</summary>
    public string[] Tenants { get; set; } = Array.Empty<string>();

    /// <summary>0–100 deterministic rollout percentage. 0 = none, 100 = all.</summary>
    public int Percentage { get; set; } = 100;
}
