using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Observability.ApplicationInsights;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaApplicationInsights(this IServiceCollection services, string? connectionString = null)
    {
        services.AddApplicationInsightsTelemetry(options =>
        {
            if (!string.IsNullOrEmpty(connectionString))
                options.ConnectionString = connectionString;
        });
        services.TryAddSingleton<IOperation, AIOperation>();
        services.TryAddSingleton<ITelemetry, AITelemetry>();
        return services;
    }
}
