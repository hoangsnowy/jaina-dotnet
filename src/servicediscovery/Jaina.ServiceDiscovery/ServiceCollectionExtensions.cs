using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.ServiceDiscovery;

namespace Jaina.ServiceDiscovery;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register service discovery + the tenant-aware endpoint provider + the
    /// tenant/correlation propagation handler. Adds a transient
    /// <see cref="TenantPropagationHandler"/> so <see cref="AddJainaHttpClient(IServiceCollection, string)"/>
    /// can chain it.
    /// </summary>
    public static IServiceCollection AddJainaServiceDiscovery(
        this IServiceCollection services,
        Action<ServiceDiscoveryOptions>? configure = null)
    {
        services.AddServiceDiscovery();

        if (configure is not null)
            services.Configure(configure);

        services.TryAddTransient<TenantPropagationHandler>();
        services.TryAddSingleton<TenantAwareServiceEndpointProvider>();
        return services;
    }

    /// <summary>
    /// Register a typed HttpClient with the full Jaina pipeline wired in one call:
    /// <list type="bullet">
    ///   <item>Service discovery — resolves <c>http://{logicalName}</c> against configured providers</item>
    ///   <item>Standard resilience handler — retry / circuit-breaker / timeout (Polly v8 defaults)</item>
    ///   <item><see cref="TenantPropagationHandler"/> — forwards <c>X-Tenant</c> + <c>X-Correlation-Id</c></item>
    /// </list>
    /// Replaces five lines of boilerplate per typed client.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddJainaMultiTenancy(b => b.FromHeader("X-Tenant"));
    /// services.AddJainaServiceDiscovery();
    /// services.AddJainaHttpClient&lt;IBillingClient, BillingClient&gt;("billing");
    /// </code>
    /// </example>
    public static IHttpClientBuilder AddJainaHttpClient<TClient, TImplementation>(
        this IServiceCollection services,
        string logicalName)
        where TClient : class
        where TImplementation : class, TClient
    {
        var builder = services.AddHttpClient<TClient, TImplementation>(c => c.BaseAddress = new Uri($"http://{logicalName}"))
            .AddServiceDiscovery();
        builder.AddStandardResilienceHandler();
        builder.AddHttpMessageHandler<TenantPropagationHandler>();
        return builder;
    }

    /// <summary>
    /// Same as <see cref="AddJainaHttpClient{TClient,TImplementation}"/> but for the
    /// non-typed-client overload — registers a named <see cref="HttpClient"/> with the
    /// full pipeline so callers resolve via <see cref="IHttpClientFactory.CreateClient(string)"/>.
    /// </summary>
    public static IHttpClientBuilder AddJainaHttpClient(
        this IServiceCollection services,
        string logicalName)
    {
        var builder = services.AddHttpClient(logicalName, c => c.BaseAddress = new Uri($"http://{logicalName}"))
            .AddServiceDiscovery();
        builder.AddStandardResilienceHandler();
        builder.AddHttpMessageHandler<TenantPropagationHandler>();
        return builder;
    }
}
