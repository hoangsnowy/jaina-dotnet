using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Data.Cqrs.Queries;

public interface IQueryBus
{
    Task<TResult> SendAsync<TQuery, TResult>(TQuery query, CancellationToken ct = default) where TQuery : IQuery<TResult>;
}

public class QueryBus : IQueryBus
{
    private readonly IServiceProvider _serviceProvider;

    public QueryBus(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public async Task<TResult> SendAsync<TQuery, TResult>(TQuery query, CancellationToken ct = default) where TQuery : IQuery<TResult>
    {
        var handler = _serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>();
        return await handler.HandleAsync(query, ct).ConfigureAwait(false);
    }
}
