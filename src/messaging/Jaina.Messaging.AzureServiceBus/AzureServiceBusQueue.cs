using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace Jaina.Messaging.AzureServiceBus;

public class AzureServiceBusQueue<T> : IQueue<T>
{
    private readonly ServiceBusClientManager _manager;
    private readonly ILogger<AzureServiceBusQueue<T>> _logger;
    private readonly string _queueName;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public AzureServiceBusQueue(ServiceBusClientManager manager, ILogger<AzureServiceBusQueue<T>> logger, string queueName)
    {
        _manager = manager;
        _logger = logger;
        _queueName = queueName;
    }

    public string Name => _queueName;

    public async Task EnqueueAsync(T message, MessageOptions? options = null, CancellationToken ct = default)
    {
        var sender = _manager.CreateSender(_queueName);
        await using var _ = sender.ConfigureAwait(false);

        var sbMessage = new ServiceBusMessage(JsonSerializer.Serialize(message, JsonOptions));
        ApplyOptions(sbMessage, options);
        await sender.SendMessageAsync(sbMessage, ct).ConfigureAwait(false);
    }

    public async Task EnqueueBatchAsync(IEnumerable<QueueMessage<T>> messages, CancellationToken ct = default)
    {
        var sender = _manager.CreateSender(_queueName);
        await using var _ = sender.ConfigureAwait(false);

        var batch = await sender.CreateMessageBatchAsync(ct).ConfigureAwait(false);
        foreach (var msg in messages)
        {
            var sbMessage = new ServiceBusMessage(JsonSerializer.Serialize(msg.Message, JsonOptions));
            ApplyOptions(sbMessage, new MessageOptions
            {
                Label = msg.Label,
                SessionId = msg.SessionId,
                MessageOrder = msg.MessageOrder,
                LastMessage = msg.LastMessage,
                Headers = msg.Headers
            });
            if (!batch.TryAddMessage(sbMessage))
            {
                await sender.SendMessagesAsync(batch, ct).ConfigureAwait(false);
                batch = await sender.CreateMessageBatchAsync(ct).ConfigureAwait(false);
                batch.TryAddMessage(sbMessage);
            }
        }
        if (batch.Count > 0)
            await sender.SendMessagesAsync(batch, ct).ConfigureAwait(false);
    }

    public async Task ScheduleAsync(T message, DateTime scheduleTime, MessageOptions? options = null, string? reason = null, CancellationToken ct = default)
    {
        var sender = _manager.CreateSender(_queueName);
        await using var _ = sender.ConfigureAwait(false);

        var sbMessage = new ServiceBusMessage(JsonSerializer.Serialize(message, JsonOptions));
        ApplyOptions(sbMessage, options);
        if (!string.IsNullOrEmpty(reason))
            sbMessage.ApplicationProperties["ScheduleReason"] = reason;
        await sender.ScheduleMessageAsync(sbMessage, new DateTimeOffset(scheduleTime, TimeSpan.Zero), ct).ConfigureAwait(false);
    }

    public Task SubscribeAsync(Action<T> consumer, CancellationToken ct = default) =>
        SubscribeAsync((msg, _) => { consumer(msg); return Task.CompletedTask; }, ct);

    public Task SubscribeAsync(Func<T, Task> consumer, CancellationToken ct = default) =>
        SubscribeAsync((msg, _) => consumer(msg), ct);

    public Task SubscribeAsync(Action<T, IDictionary<string, object>> consumer, CancellationToken ct = default) =>
        SubscribeAsync((msg, headers) => { consumer(msg, headers); return Task.CompletedTask; }, ct);

    public async Task SubscribeAsync(Func<T, IDictionary<string, object>, Task> consumer, CancellationToken ct = default)
    {
        var processor = _manager.CreateProcessor(_queueName);
        processor.ProcessMessageAsync += async args =>
        {
            var message = JsonSerializer.Deserialize<T>(args.Message.Body.ToString(), JsonOptions)!;
            var headers = args.Message.ApplicationProperties.ToDictionary(k => k.Key, v => v.Value);
            await consumer(message, headers);
            await args.CompleteMessageAsync(args.Message, ct).ConfigureAwait(false);
        };
        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Error processing message from queue {Queue}", _queueName);
            return Task.CompletedTask;
        };
        await processor.StartProcessingAsync(ct).ConfigureAwait(false);
    }

    private static void ApplyOptions(ServiceBusMessage sbMessage, MessageOptions? options)
    {
        if (options is null) return;
        if (!string.IsNullOrEmpty(options.Label)) sbMessage.Subject = options.Label;
        if (!string.IsNullOrEmpty(options.SessionId)) sbMessage.SessionId = options.SessionId;
        if (options.MessageOrder >= 0) sbMessage.ApplicationProperties[MessagePropertyNames.MessageOrder] = options.MessageOrder;
        sbMessage.ApplicationProperties[MessagePropertyNames.LastMessage] = options.LastMessage;
        foreach (var kvp in options.Headers)
            sbMessage.ApplicationProperties[kvp.Key] = kvp.Value;
    }
}
