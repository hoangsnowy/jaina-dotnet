using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Jaina.BackgroundJobs.Quartz;

/// <summary>
/// Bridges a Quartz <see cref="IJob"/> invocation onto the user's
/// <see cref="IBackgroundJob{TPayload}"/>. Resolves the user's job via DI, deserializes the
/// payload from the trigger's JobDataMap, and forwards the cancellation token.
/// </summary>
internal sealed class QuartzJobAdapter<TJob, TPayload> : IJob
    where TJob : IBackgroundJob<TPayload>
{
    private readonly IServiceProvider _services;

    public QuartzJobAdapter(IServiceProvider services)
    {
        _services = services;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var raw = context.MergedJobDataMap.GetString("payload");
        var payload = string.IsNullOrEmpty(raw)
            ? default!
            : JsonSerializer.Deserialize<TPayload>(raw)!;

        using var scope = _services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<TJob>();
        await job.ExecuteAsync(payload, context.CancellationToken);
    }
}
