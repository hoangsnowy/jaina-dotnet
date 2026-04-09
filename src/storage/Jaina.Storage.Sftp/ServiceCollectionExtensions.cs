using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Storage.Sftp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaSftpStorage(this IServiceCollection services, Action<SftpStorageOptions> configure)
    {
        services.AddOptions<SftpStorageOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<IFileStorage, SftpFileStorage>();
        return services;
    }
}
