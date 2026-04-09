using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Storage.AzureBlob;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaAzureBlobStorage(this IServiceCollection services, Action<AzureBlobStorageOptions> configure)
    {
        services.AddOptions<AzureBlobStorageOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<IFileStorage, AzureBlobFileStorage>();
        return services;
    }
}
