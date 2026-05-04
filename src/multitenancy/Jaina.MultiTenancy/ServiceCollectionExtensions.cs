using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.MultiTenancy;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the multi-tenancy core: scoped <see cref="ITenantContext"/> and a builder
    /// for adding resolver strategies. Subsequent <c>FromHeader / FromClaim / FromHost /
    /// FromRoute</c> calls register resolvers in priority order.
    /// </summary>
    public static IServiceCollection AddJainaMultiTenancy(
        this IServiceCollection services,
        Action<JainaMultiTenancyBuilder>? configure = null)
    {
        services.TryAddScoped<ITenantContext, TenantContext>();
        var builder = new JainaMultiTenancyBuilder(services);
        configure?.Invoke(builder);
        return services;
    }
}

public static class ApplicationBuilderExtensions
{
    /// <summary>Insert the tenant resolution middleware. Place after auth, before endpoints.</summary>
    public static IApplicationBuilder UseJainaTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
