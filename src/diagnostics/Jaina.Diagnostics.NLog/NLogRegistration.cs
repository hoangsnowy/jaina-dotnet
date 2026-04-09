using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace Jaina.Diagnostics.NLog;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaNLog(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddNLog();
        });
        return services;
    }
}
