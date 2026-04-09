using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using global::RabbitMQ.Client;

namespace Jaina.Messaging.RabbitMQ;

public class RabbitMQConnectionManager : IAsyncDisposable
{
    private readonly RabbitMQOptions _options;
    private readonly ILogger<RabbitMQConnectionManager> _logger;
    private IConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RabbitMQConnectionManager(IOptions<RabbitMQOptions> options, ILogger<RabbitMQConnectionManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (_connection is { IsOpen: true }) return _connection;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_connection is { IsOpen: true }) return _connection;

            var factory = new ConnectionFactory();
            if (!string.IsNullOrEmpty(_options.ConnectionString))
            {
                factory.Uri = new Uri(_options.ConnectionString);
            }
            else
            {
                factory.HostName = _options.HostName;
                factory.Port = _options.Port;
                if (!string.IsNullOrEmpty(_options.Username)) factory.UserName = _options.Username!;
                if (!string.IsNullOrEmpty(_options.Password)) factory.Password = _options.Password!;
                if (!string.IsNullOrEmpty(_options.VirtualHost)) factory.VirtualHost = _options.VirtualHost!;
            }

            _connection = await factory.CreateConnectionAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("RabbitMQ connection established to {Host}", factory.HostName);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            _connection.Dispose();
        }
    }
}
