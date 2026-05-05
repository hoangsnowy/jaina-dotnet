using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;

namespace Jaina.FeatureFlags;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register Microsoft.FeatureManagement plus the Jaina targeting filters
    /// (<c>Jaina.Tenant</c>, <c>Jaina.User</c>) so config-driven tenant- and user-based
    /// rollouts work out of the box. Also registers the observable
    /// <see cref="IJainaFeatureManager"/> wrapper that emits an OTEL span per evaluation.
    /// </summary>
    /// <example>
    /// <code>
    /// // Program.cs
    /// services.AddJainaMultiTenancy(b => b.FromHeader("X-Tenant"));
    /// services.AddJainaFeatureFlags();
    ///
    /// // appsettings.json
    /// "FeatureManagement": {
    ///   "BetaPricing":  { "EnabledFor": [ { "Name": "Jaina.Tenant", "Parameters": { "Tenants": [ "acme" ] } } ] },
    ///   "NewDashboard": { "EnabledFor": [ { "Name": "Jaina.User",   "Parameters": { "Roles": [ "beta-tester" ], "Percentage": 25 } } ] }
    /// }
    ///
    /// // Inject and gate code
    /// public class CheckoutHandler(IJainaFeatureManager flags)
    /// {
    ///     public async Task DoCheckoutAsync(...)
    ///     {
    ///         if (await flags.IsEnabledAsync("BetaPricing")) { ... }
    ///     }
    /// }
    /// </code>
    /// </example>
    public static IServiceCollection AddJainaFeatureFlags(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddFeatureManagement()
            .AddFeatureFilter<TenantTargetingFilter>()
            .AddFeatureFilter<UserTargetingFilter>();

        services.TryAddScoped<IJainaFeatureManager, JainaFeatureManager>();
        return services;
    }
}
