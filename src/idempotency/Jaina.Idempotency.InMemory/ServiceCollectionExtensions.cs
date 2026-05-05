using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Idempotency.InMemory;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register an in-memory <see cref="IIdempotencyStore"/>. Adds <c>IMemoryCache</c> if not
    /// already registered. Pass a configure action to tweak <see cref="IdempotencyOptions"/>.
    /// </summary>
    public static IServiceCollection AddJainaInMemoryIdempotency(
        this IServiceCollection services,
        Action<IdempotencyOptions>? configure = null)
    {
        services.AddMemoryCache();
        services.AddOptions<IdempotencyOptions>();
        if (configure is not null)
            services.Configure(configure);

        services.TryAddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        return services;
    }
}
