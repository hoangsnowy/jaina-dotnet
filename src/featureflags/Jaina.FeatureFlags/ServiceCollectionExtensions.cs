using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace Jaina.FeatureFlags;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register Microsoft.FeatureManagement and bind feature flag definitions from
    /// configuration (the <c>FeatureManagement</c> section by default).
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddJainaFeatureFlags();
    /// // appsettings.json
    /// // "FeatureManagement": { "NewCheckout": true, "BetaPricing": { "EnabledFor": [{...}] } }
    ///
    /// // Inject IFeatureManager and gate code:
    /// if (await featureManager.IsEnabledAsync("NewCheckout")) { ... }
    /// </code>
    /// </example>
    public static IServiceCollection AddJainaFeatureFlags(this IServiceCollection services)
    {
        services.AddFeatureManagement();
        return services;
    }
}
