using System.Text.Json;
using Quartz;

namespace Jaina.BackgroundJobs.Quartz;

/// <summary>
/// Quartz.NET implementation of <see cref="IBackgroundJobScheduler"/>. Wraps the user's
/// <see cref="IBackgroundJob{TPayload}"/> in a <see cref="QuartzJobAdapter{TJob, TPayload}"/>
/// (created on the fly via DI), serializes the payload as JSON into the JobDataMap, and
/// schedules a one-shot or cron trigger.
/// </summary>
public sealed class QuartzBackgroundJobScheduler : IBackgroundJobScheduler
{
    private readonly ISchedulerFactory _factory;

    public QuartzBackgroundJobScheduler(ISchedulerFactory factory)
    {
        _factory = factory;
    }

    public async Task ScheduleAsync<TJob, TPayload>(TPayload payload, JobOptions? options = null, CancellationToken ct = default)
        where TJob : IBackgroundJob<TPayload>
    {
        var scheduler = await _factory.GetScheduler(ct);
        var jobKey = new JobKey($"jaina.{typeof(TJob).Name}.{Guid.NewGuid():N}", options?.Group ?? "jaina");

        var jobDetail = JobBuilder.Create<QuartzJobAdapter<TJob, TPayload>>()
            .WithIdentity(jobKey)
            .UsingJobData("payload", JsonSerializer.Serialize(payload))
            .UsingJobData("payloadType", typeof(TPayload).AssemblyQualifiedName ?? typeof(TPayload).FullName!)
            .Build();

        var triggerBuilder = TriggerBuilder.Create()
            .WithIdentity($"trigger.{jobKey.Name}", jobKey.Group);

        if (options?.RunAt is { } runAt)
            triggerBuilder.StartAt(runAt);
        else if (options?.Delay is { } delay)
            triggerBuilder.StartAt(DateTimeOffset.UtcNow + delay);
        else
            triggerBuilder.StartNow();

        await scheduler.ScheduleJob(jobDetail, triggerBuilder.Build(), ct);
    }

    public async Task ScheduleRecurringAsync<TJob, TPayload>(string name, string cronExpression, TPayload payload, CancellationToken ct = default)
        where TJob : IBackgroundJob<TPayload>
    {
        var scheduler = await _factory.GetScheduler(ct);
        var jobKey = new JobKey(name, "jaina.recurring");

        // Replace any existing schedule with the same name
        await scheduler.DeleteJob(jobKey, ct);

        var jobDetail = JobBuilder.Create<QuartzJobAdapter<TJob, TPayload>>()
            .WithIdentity(jobKey)
            .UsingJobData("payload", JsonSerializer.Serialize(payload))
            .UsingJobData("payloadType", typeof(TPayload).AssemblyQualifiedName ?? typeof(TPayload).FullName!)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"trigger.{name}", "jaina.recurring")
            .WithCronSchedule(cronExpression)
            .Build();

        await scheduler.ScheduleJob(jobDetail, trigger, ct);
    }

    public async Task RemoveRecurringAsync(string name, CancellationToken ct = default)
    {
        var scheduler = await _factory.GetScheduler(ct);
        await scheduler.DeleteJob(new JobKey(name, "jaina.recurring"), ct);
    }
}
