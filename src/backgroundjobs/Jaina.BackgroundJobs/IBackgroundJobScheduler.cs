namespace Jaina.BackgroundJobs;

/// <summary>
/// A background job — implemented by user code, executed by the registered scheduler.
/// </summary>
/// <typeparam name="TPayload">Serializable payload describing the work.</typeparam>
public interface IBackgroundJob<TPayload>
{
    Task ExecuteAsync(TPayload payload, CancellationToken ct);
}

/// <summary>Options for one-shot job scheduling.</summary>
public sealed class JobOptions
{
    /// <summary>Run after this delay. Null = run immediately.</summary>
    public TimeSpan? Delay { get; set; }

    /// <summary>Run at this UTC time. Takes precedence over <see cref="Delay"/>.</summary>
    public DateTimeOffset? RunAt { get; set; }

    /// <summary>Optional logical group / queue name (provider-specific semantics).</summary>
    public string? Group { get; set; }
}

/// <summary>
/// Schedule background jobs. Provider-agnostic surface — Quartz / Hangfire / etc. plug in
/// behind the interface. <see cref="ScheduleAsync{TJob,TPayload}"/> queues a single
/// execution; <see cref="ScheduleRecurringAsync{TJob,TPayload}"/> registers a cron-driven
/// recurring job.
/// </summary>
public interface IBackgroundJobScheduler
{
    /// <summary>Queue a one-shot job.</summary>
    Task ScheduleAsync<TJob, TPayload>(TPayload payload, JobOptions? options = null, CancellationToken ct = default)
        where TJob : IBackgroundJob<TPayload>;

    /// <summary>
    /// Register or update a recurring job. <paramref name="name"/> uniquely identifies the
    /// schedule — re-registering replaces the prior entry. <paramref name="cronExpression"/>
    /// is a Quartz-style 7-field cron (or 5-field if the provider supports both).
    /// </summary>
    Task ScheduleRecurringAsync<TJob, TPayload>(string name, string cronExpression, TPayload payload, CancellationToken ct = default)
        where TJob : IBackgroundJob<TPayload>;

    /// <summary>Remove a recurring job by name.</summary>
    Task RemoveRecurringAsync(string name, CancellationToken ct = default);
}
