namespace Jaina.Messaging.Outbox;

/// <summary>
/// Configuration for the outbox relay. Tune the polling cadence, batch size, and retry
/// policy to match your throughput and broker characteristics.
/// </summary>
public sealed class OutboxOptions
{
    /// <summary>How often the relay polls for pending messages. Defaults to 1s.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Max messages claimed per polling iteration. Defaults to 100.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Max dispatch attempts before a message is parked as poison. Defaults to 10.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Initial backoff between retries; doubles up to <see cref="MaxBackoff"/>.</summary>
    public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Upper bound on retry backoff. Defaults to 5 minutes.</summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(5);
}
