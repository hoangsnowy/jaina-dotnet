using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jaina.Messaging.Outbox;

/// <summary>
/// Background service that polls <see cref="IOutboxStore"/> for due messages, dispatches
/// each via <see cref="IOutboxDispatcher"/>, and marks the result. Failures are retried with
/// exponential backoff up to <see cref="OutboxOptions.MaxAttempts"/>.
/// </summary>
public sealed class OutboxRelay : BackgroundService
{
    private readonly IOutboxStore _store;
    private readonly IOutboxDispatcher _dispatcher;
    private readonly OutboxOptions _opts;
    private readonly ILogger<OutboxRelay> _logger;

    public OutboxRelay(
        IOutboxStore store,
        IOutboxDispatcher dispatcher,
        IOptions<OutboxOptions> opts,
        ILogger<OutboxRelay> logger)
    {
        _store = store;
        _dispatcher = dispatcher;
        _opts = opts.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox relay starting (poll {Interval}, batch {Batch})",
            _opts.PollingInterval, _opts.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox relay iteration failed");
            }

            try
            {
                await Task.Delay(_opts.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("Outbox relay stopped");
    }

    /// <summary>
    /// Process one polling iteration. Exposed for tests so callers can drive the loop
    /// deterministically without spinning up a real host.
    /// </summary>
    public async Task ProcessOnceAsync(CancellationToken ct = default)
    {
        var batch = await _store.ClaimBatchAsync(_opts.BatchSize, ct);
        if (batch.Count == 0) return;

        foreach (var msg in batch)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _dispatcher.DispatchAsync(msg, ct);
                await _store.MarkProcessedAsync(msg.Id, ct);
            }
            catch (Exception ex)
            {
                var nextAttempt = ComputeNextAttempt(msg.Attempts + 1);
                _logger.LogWarning(ex,
                    "Outbox dispatch failed for message {Id} (attempt {Attempts}); retrying at {NextAttempt}",
                    msg.Id, msg.Attempts + 1, nextAttempt);
                await _store.MarkFailedAsync(msg.Id, ex.Message, nextAttempt, ct);
            }
        }
    }

    private DateTimeOffset ComputeNextAttempt(int nextAttemptNumber)
    {
        // Exponential: initial * 2^(n-1), capped at MaxBackoff
        var delay = TimeSpan.FromMilliseconds(
            Math.Min(
                _opts.InitialBackoff.TotalMilliseconds * Math.Pow(2, nextAttemptNumber - 1),
                _opts.MaxBackoff.TotalMilliseconds));
        return DateTimeOffset.UtcNow + delay;
    }
}
