using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ServiceDiscovery;

namespace Jaina.ServiceDiscovery;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register service discovery for the application. Mirrors
    /// <c>Microsoft.Extensions.ServiceDiscovery.AddServiceDiscovery</c> with Jaina-specific
    /// defaults (Configuration + DNS + Pass-through resolvers enabled).
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddJainaServiceDiscovery();
    /// services.AddHttpClient&lt;IBillingClient, BillingClient&gt;(c => c.BaseAddress = new("http://billing"))
    ///         .AddServiceDiscovery();
    /// </code>
    /// </example>
    public static IServiceCollection AddJainaServiceDiscovery(
        this IServiceCollection services,
        Action<ServiceDiscoveryOptions>? configure = null)
    {
        services.AddServiceDiscovery();

        if (configure is not null)
            services.Configure(configure);

        return services;
    }
}
