using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Storage.AzureFileShare;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaAzureFileShareStorage(this IServiceCollection services, Action<AzureFileShareOptions> configure)
    {
        services.AddOptions<AzureFileShareOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<IFileStorage, AzureFileShareStorage>();
        return services;
    }
}
