using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaUnitOfWork<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.TryAddScoped<IUnitOfWork, EfUnitOfWork<TContext>>();
        return services;
    }
}
