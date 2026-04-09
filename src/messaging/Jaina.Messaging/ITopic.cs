namespace Jaina.Messaging;

public interface ITopic<T>
{
    string Name { get; }

    Task PublishAsync(T message, MessageOptions? options = null, CancellationToken ct = default);

    Task SubscribeAsync(Action<T> consumer, string subscriptionName, CancellationToken ct = default, params (string Name, string Label)[] filters);
    Task SubscribeAsync(Func<T, Task> consumer, string subscriptionName, CancellationToken ct = default, params (string Name, string Label)[] filters);
    Task SubscribeAsync(Action<T, IDictionary<string, object>> consumer, string subscriptionName, CancellationToken ct = default, params (string Name, string Label)[] filters);
    Task SubscribeAsync(Func<T, IDictionary<string, object>, Task> consumer, string subscriptionName, CancellationToken ct = default, params (string Name, string Label)[] filters);
}
