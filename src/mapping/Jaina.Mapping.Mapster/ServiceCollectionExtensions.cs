using Jaina.Mapping;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Mapping.Mapster;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMapper"/> backed by Mapster.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">Optional delegate to customise mapping rules via <see cref="TypeAdapterConfig"/>.</param>
    public static IServiceCollection AddJainaMapster(
        this IServiceCollection services,
        Action<TypeAdapterConfig>? configure = null)
    {
        var config = TypeAdapterConfig.GlobalSettings.Clone();
        configure?.Invoke(config);

        services.TryAddSingleton(config);
        services.TryAddSingleton<IMapper, MapsterMapper>();
        return services;
    }
}
