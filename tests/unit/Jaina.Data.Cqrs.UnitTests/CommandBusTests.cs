using Jaina.Data.Cqrs.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Data.Cqrs.UnitTests;

public class CommandBusTests
{
    private record PingCommand(string Message) : ICommand;
    private record EchoCommand(string Text) : ICommand<string>;

    // Concrete handlers — NSubstitute cannot proxy contravariant generic interfaces (in TCommand)
    private class PingHandler : ICommandHandler<PingCommand>
    {
        public int CallCount { get; private set; }
        public PingCommand? LastCommand { get; private set; }

        public Task HandleAsync(PingCommand command, CancellationToken ct = default)
        {
            CallCount++;
            LastCommand = command;
            return Task.CompletedTask;
        }
    }

    private class EchoHandler : ICommandHandler<EchoCommand, string>
    {
        public Task<string> HandleAsync(EchoCommand command, CancellationToken ct = default) =>
            Task.FromResult(command.Text.ToUpper());
    }

    [Fact]
    public async Task SendAsync_VoidCommand_InvokesHandler()
    {
        // Arrange
        var handler = new PingHandler();
        var services = new ServiceCollection();
        services.AddJainaCqrs();
        services.AddTransient<ICommandHandler<PingCommand>>(_ => handler);
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<ICommandBus>();
        var cmd = new PingCommand("hello");

        // Act
        await bus.SendAsync(cmd);

        // Assert
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(cmd, handler.LastCommand);
    }

    [Fact]
    public async Task SendAsync_CommandWithResult_ReturnsHandlerResult()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJainaCqrs();
        services.AddTransient<ICommandHandler<EchoCommand, string>, EchoHandler>();
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<ICommandBus>();

        // Act
        var result = await bus.SendAsync<EchoCommand, string>(new EchoCommand("jaina"));

        // Assert
        Assert.Equal("JAINA", result);
    }

    [Fact]
    public async Task SendAsync_NoHandlerRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJainaCqrs();
        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<ICommandBus>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => bus.SendAsync(new PingCommand("test")));
    }
}
