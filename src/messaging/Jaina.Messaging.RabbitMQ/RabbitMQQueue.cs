using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;

namespace Jaina.Messaging.RabbitMQ;

public class RabbitMQQueue<T> : IQueue<T>
{
    private readonly RabbitMQConnectionManager _manager;
    private readonly ILogger<RabbitMQQueue<T>> _logger;
    private readonly string _queueName;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public RabbitMQQueue(RabbitMQConnectionManager manager, ILogger<RabbitMQQueue<T>> logger, string queueName)
    {
        _manager = manager;
        _logger = logger;
        _queueName = queueName;
    }

    public string Name => _queueName;

    public async Task EnqueueAsync(T message, MessageOptions? options = null, CancellationToken ct = default)
    {
        var conn = await _manager.GetConnectionAsync(ct).ConfigureAwait(false);
        using var channel = await conn.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);
        await channel.QueueDeclareAsync(_queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct).ConfigureAwait(false);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOptions));
        var props = new BasicProperties { Persistent = true };

        if (options is not null)
        {
            foreach (var kvp in options.Headers)
            {
                props.Headers ??= new Dictionary<string, object?>();
                props.Headers[kvp.Key] = kvp.Value;
            }

            if (!string.IsNullOrEmpty(options.Label))
                (props.Headers ??= new Dictionary<string, object?>())[MessagePropertyNames.Label] = Encoding.UTF8.GetBytes(options.Label);
        }

        await channel.BasicPublishAsync("", _queueName, false, props, body, ct).ConfigureAwait(false);
    }

    public async Task EnqueueBatchAsync(IEnumerable<QueueMessage<T>> messages, CancellationToken ct = default)
    {
        foreach (var msg in messages)
        {
            var options = new MessageOptions
            {
                Label = msg.Label,
                SessionId = msg.SessionId,
                MessageOrder = msg.MessageOrder,
                LastMessage = msg.LastMessage,
                Headers = msg.Headers
            };
            await EnqueueAsync(msg.Message, options, ct).ConfigureAwait(false);
        }
    }

    public Task ScheduleAsync(T message, DateTime scheduleTime, MessageOptions? options = null, string? reason = null, CancellationToken ct = default)
    {
        _logger.LogWarning("RabbitMQ does not natively support scheduled messages. Consider using a delayed message exchange plugin.");
        return EnqueueAsync(message, options, ct);
    }

    public async Task SubscribeAsync(Action<T> consumer, CancellationToken ct = default) =>
        await SubscribeAsync((msg, _) => consumer(msg), ct).ConfigureAwait(false);

    public async Task SubscribeAsync(Func<T, Task> consumer, CancellationToken ct = default) =>
        await SubscribeAsync((msg, _) => consumer(msg), ct).ConfigureAwait(false);

    public async Task SubscribeAsync(Action<T, IDictionary<string, object>> consumer, CancellationToken ct = default) =>
        await SubscribeAsync((msg, headers) => { consumer(msg, headers); return Task.CompletedTask; }, ct).ConfigureAwait(false);

    public async Task SubscribeAsync(Func<T, IDictionary<string, object>, Task> consumer, CancellationToken ct = default)
    {
        var conn = await _manager.GetConnectionAsync(ct).ConfigureAwait(false);
        var channel = await conn.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);
        await channel.QueueDeclareAsync(_queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct).ConfigureAwait(false);

        var asyncConsumer = new AsyncEventingBasicConsumer(channel);
        asyncConsumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<T>(body, JsonOptions)!;
                var headers = ea.BasicProperties.Headers?.ToDictionary(
                    k => k.Key,
                    v => v.Value ?? (object)"") ?? new Dictionary<string, object>();

                await consumer(message, headers);
                await channel.BasicAckAsync(ea.DeliveryTag, false, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue {Queue}", _queueName);
                await channel.BasicNackAsync(ea.DeliveryTag, false, true, ct).ConfigureAwait(false);
            }
        };

        await channel.BasicConsumeAsync(_queueName, autoAck: false, consumer: asyncConsumer, cancellationToken: ct).ConfigureAwait(false);
    }
}
