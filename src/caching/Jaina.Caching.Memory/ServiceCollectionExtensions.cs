using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Caching.Memory;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaMemoryCache(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.TryAddSingleton<ICache, MemoryCache>();
        return services;
    }
}
