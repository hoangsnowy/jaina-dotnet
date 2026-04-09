using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace Jaina.Messaging.AzureServiceBus;

public class ServiceBusClientManager : IAsyncDisposable
{
    private readonly ServiceBusClient _client;

    public ServiceBusClientManager(IOptions<ServiceBusOptions> options)
    {
        _client = new ServiceBusClient(options.Value.ConnectionString);
    }

    public ServiceBusClient Client => _client;

    public ServiceBusSender CreateSender(string queueOrTopicName) =>
        _client.CreateSender(queueOrTopicName);

    public ServiceBusProcessor CreateProcessor(string queueName, ServiceBusProcessorOptions? options = null) =>
        _client.CreateProcessor(queueName, options ?? new ServiceBusProcessorOptions());

    public ServiceBusProcessor CreateProcessor(string topicName, string subscriptionName, ServiceBusProcessorOptions? options = null) =>
        _client.CreateProcessor(topicName, subscriptionName, options ?? new ServiceBusProcessorOptions());

    public async ValueTask DisposeAsync() => await _client.DisposeAsync().ConfigureAwait(false);
}
