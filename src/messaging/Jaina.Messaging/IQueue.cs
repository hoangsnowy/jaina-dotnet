namespace Jaina.Messaging;

public interface IQueue<T>
{
    string Name { get; }

    Task EnqueueAsync(T message, MessageOptions? options = null, CancellationToken ct = default);
    Task EnqueueBatchAsync(IEnumerable<QueueMessage<T>> messages, CancellationToken ct = default);
    Task ScheduleAsync(T message, DateTime scheduleTime, MessageOptions? options = null, string? reason = null, CancellationToken ct = default);

    Task SubscribeAsync(Action<T> consumer, CancellationToken ct = default);
    Task SubscribeAsync(Func<T, Task> consumer, CancellationToken ct = default);
    Task SubscribeAsync(Action<T, IDictionary<string, object>> consumer, CancellationToken ct = default);
    Task SubscribeAsync(Func<T, IDictionary<string, object>, Task> consumer, CancellationToken ct = default);
}
