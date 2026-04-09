using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Storage.Local;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaLocalStorage(this IServiceCollection services, Action<LocalStorageOptions> configure)
    {
        services.AddOptions<LocalStorageOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<IFileStorage, LocalFileStorage>();
        return services;
    }
}
