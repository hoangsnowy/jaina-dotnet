using Jaina.Data.Cqrs.Commands;
using Jaina.Data.Cqrs.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Data.Cqrs;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaCqrs(this IServiceCollection services)
    {
        services.TryAddSingleton<ICommandBus, CommandBus>();
        services.TryAddSingleton<IQueryBus, QueryBus>();
        return services;
    }

    public static IServiceCollection AddCommandHandler<TCommand, THandler>(this IServiceCollection services)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        services.AddTransient<ICommandHandler<TCommand>, THandler>();
        return services;
    }

    public static IServiceCollection AddQueryHandler<TQuery, TResult, THandler>(this IServiceCollection services)
        where TQuery : IQuery<TResult>
        where THandler : class, IQueryHandler<TQuery, TResult>
    {
        services.AddTransient<IQueryHandler<TQuery, TResult>, THandler>();
        return services;
    }
}
