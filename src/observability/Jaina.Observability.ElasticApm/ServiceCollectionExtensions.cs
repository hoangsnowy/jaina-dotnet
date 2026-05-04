using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Observability.ElasticApm;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaElasticApm(this IServiceCollection services)
    {
        services.TryAddSingleton<IOperation, ApmOperation>();
        services.TryAddSingleton<ITelemetry, ApmTelemetry>();
        return services;
    }
}
