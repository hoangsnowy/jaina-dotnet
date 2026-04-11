using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Data.Dapper;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaDapper<TConnection>(
        this IServiceCollection services,
        Func<IServiceProvider, TConnection> connectionFactory)
        where TConnection : class, IDbConnection
    {
        services.TryAddScoped<IDbConnection>(connectionFactory);
        return services;
    }
}
