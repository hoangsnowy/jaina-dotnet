using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Jaina.Caching.Redis;

public class RedisConnectionManager
{
    private static readonly object ReconnectLock = new();
    private readonly string _connectionString;
    private long _lastReconnectTicks = DateTimeOffset.MinValue.UtcTicks;
    private DateTimeOffset _firstError = DateTimeOffset.MinValue;
    private DateTimeOffset _previousError = DateTimeOffset.MinValue;
    private Lazy<ConnectionMultiplexer> _multiplexer;

    private static readonly TimeSpan ReconnectMinFrequency = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ReconnectErrorThreshold = TimeSpan.FromSeconds(30);

    public RedisConnectionManager(IOptions<RedisCacheOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
        _multiplexer = CreateMultiplexer();
    }

    public ConnectionMultiplexer Connection => _multiplexer.Value;

    private Lazy<ConnectionMultiplexer> CreateMultiplexer() =>
        new(() => ConnectionMultiplexer.Connect(_connectionString));

    public void ForceReconnect()
    {
        var utcNow = DateTimeOffset.UtcNow;
        var previousTicks = Interlocked.Read(ref _lastReconnectTicks);
        var previousReconnect = new DateTimeOffset(previousTicks, TimeSpan.Zero);
        var elapsed = utcNow - previousReconnect;

        if (elapsed <= ReconnectMinFrequency) return;

        lock (ReconnectLock)
        {
            utcNow = DateTimeOffset.UtcNow;
            elapsed = utcNow - previousReconnect;

            if (_firstError == DateTimeOffset.MinValue)
            {
                _firstError = utcNow;
                _previousError = utcNow;
                return;
            }

            if (elapsed < ReconnectMinFrequency) return;

            var elapsedSinceFirst = utcNow - _firstError;
            var elapsedSinceMostRecent = utcNow - _previousError;
            var shouldReconnect = elapsedSinceFirst >= ReconnectErrorThreshold && elapsedSinceMostRecent <= ReconnectErrorThreshold;

            _previousError = utcNow;

            if (shouldReconnect)
            {
                _firstError = DateTimeOffset.MinValue;
                _previousError = DateTimeOffset.MinValue;

                var old = _multiplexer;
                try { old.Value.Close(); } catch { /* ignore */ }
                _multiplexer = CreateMultiplexer();
                Interlocked.Exchange(ref _lastReconnectTicks, utcNow.UtcTicks);
            }
        }
    }
}
