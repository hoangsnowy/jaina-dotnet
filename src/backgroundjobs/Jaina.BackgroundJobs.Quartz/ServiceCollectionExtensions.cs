using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartz;

namespace Jaina.BackgroundJobs.Quartz;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register Quartz.NET (in-memory store) and the Jaina <see cref="IBackgroundJobScheduler"/>.
    /// Caller registers their own job classes (<c>services.AddScoped&lt;OrderEnricherJob&gt;()</c>)
    /// before resolving the scheduler.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddJainaQuartzBackgroundJobs();
    /// services.AddScoped&lt;OrderEnricherJob&gt;();
    /// // ...
    /// await scheduler.ScheduleAsync&lt;OrderEnricherJob, OrderEnvelope&gt;(envelope);
    /// await scheduler.ScheduleRecurringAsync&lt;NightlyDigestJob, NightlyArgs&gt;(
    ///     "nightly-digest", "0 0 2 * * ?", new NightlyArgs(...));
    /// </code>
    /// </example>
    public static IServiceCollection AddJainaQuartzBackgroundJobs(this IServiceCollection services)
    {
        services.AddQuartz();
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
        services.TryAddSingleton<IBackgroundJobScheduler, QuartzBackgroundJobScheduler>();
        return services;
    }
}
