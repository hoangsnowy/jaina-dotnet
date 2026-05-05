using Grpc.AspNetCore.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Grpc;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register Jaina's gRPC interceptors (Logging + Correlation) globally on every gRPC
    /// service. Caller still calls <c>services.AddGrpc()</c> + <c>app.MapGrpcService&lt;...&gt;()</c>.
    /// </summary>
    public static IServiceCollection AddJainaGrpc(this IServiceCollection services)
    {
        services.TryAddSingleton<LoggingInterceptor>();
        services.TryAddSingleton<CorrelationInterceptor>();

        services.Configure<GrpcServiceOptions>(o =>
        {
            o.Interceptors.Add<CorrelationInterceptor>();
            o.Interceptors.Add<LoggingInterceptor>();
        });

        return services;
    }
}
