using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Security.KeyVault;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaKeyVault(this IServiceCollection services, Action<KeyVaultOptions> configure)
    {
        services.AddOptions<KeyVaultOptions>()
            .Configure(configure)
            .ValidateOnStart();

        services.TryAddSingleton<IKeyVaultService, KeyVaultService>();
        return services;
    }
}
