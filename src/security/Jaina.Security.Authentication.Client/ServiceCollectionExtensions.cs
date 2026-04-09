using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Security.Authentication.Client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaAuthorizationClient(this IServiceCollection services, Action<AuthorizationClientOptions> configure)
    {
        services.AddOptions<AuthorizationClientOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
