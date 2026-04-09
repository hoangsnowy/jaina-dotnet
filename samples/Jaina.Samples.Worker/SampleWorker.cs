using Jaina.Caching;

namespace Jaina.Samples.Worker;

public class SampleWorker : BackgroundService
{
    private readonly ICache _cache;
    private readonly ILogger<SampleWorker> _logger;

    public SampleWorker(ICache cache, ILogger<SampleWorker> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var counter = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            counter++;
            _cache.Set("worker:heartbeat", DateTime.UtcNow.ToString("O"), TimeSpan.FromMinutes(1));
            _logger.LogInformation("Worker heartbeat #{Counter} at {Time}", counter, DateTime.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
