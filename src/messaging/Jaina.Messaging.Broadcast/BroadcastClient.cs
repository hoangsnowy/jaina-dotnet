using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jaina.Messaging.Broadcast;

public class BroadcastOptions
{
    public string ConnectionString { get; set; } = "";
    public string TopicName { get; set; } = "broadcast";
}

public class BroadcastClient : IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    private readonly ILogger<BroadcastClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public BroadcastClient(IOptions<BroadcastOptions> options, ILogger<BroadcastClient> logger)
    {
        _logger = logger;
        _client = new ServiceBusClient(options.Value.ConnectionString);
        _sender = _client.CreateSender(options.Value.TopicName);
    }

    public async Task PublishAsync<T>(BroadcastMessage<T> message, CancellationToken ct = default)
    {
        var sbMessage = new ServiceBusMessage(JsonSerializer.Serialize(message.Payload, JsonOptions))
        {
            Subject = message.Channel
        };
        foreach (var kvp in message.Headers)
            sbMessage.ApplicationProperties[kvp.Key] = kvp.Value;

        await _sender.SendMessageAsync(sbMessage, ct).ConfigureAwait(false);
        _logger.LogInformation("Broadcast message published to channel {Channel}", message.Channel);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync().ConfigureAwait(false);
        await _client.DisposeAsync().ConfigureAwait(false);
    }
}
