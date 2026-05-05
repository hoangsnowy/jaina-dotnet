using Jaina.Data.Cqrs.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Data.Cqrs.UnitTests;

public class QueryBusTests
{
    private record GetUserQuery(int Id) : IQuery<string?>;

    private class GetUserHandler : IQueryHandler<GetUserQuery, string?>
    {
        public Task<string?> HandleAsync(GetUserQuery query, CancellationToken ct = default) =>
            Task.FromResult<string?>($"user-{query.Id}");
    }

    private class NullUserHandler : IQueryHandler<GetUserQuery, string?>
    {
        public Task<string?> HandleAsync(GetUserQuery query, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);
    }

    [Fact]
    public async Task SendAsync_ValidQuery_ReturnsHandlerResult()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJainaCqrs();
        services.AddTransient<IQueryHandler<GetUserQuery, string?>, GetUserHandler>();
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IQueryBus>();

        // Act
        var result = await bus.SendAsync<GetUserQuery, string?>(new GetUserQuery(7));

        // Assert
        Assert.Equal("user-7", result);
    }

    [Fact]
    public async Task SendAsync_HandlerReturnsNull_ReturnsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJainaCqrs();
        services.AddTransient<IQueryHandler<GetUserQuery, string?>, NullUserHandler>();
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IQueryBus>();

        // Act
        var result = await bus.SendAsync<GetUserQuery, string?>(new GetUserQuery(99));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SendAsync_NoHandlerRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJainaCqrs();
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IQueryBus>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => bus.SendAsync<GetUserQuery, string?>(new GetUserQuery(1)));
    }
}
