using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Jaina.Resilience;

/// <summary>
/// Fluent builder for registering named resilience pipelines.
/// Returned from <c>services.AddJainaResilience(builder => ...)</c>.
/// </summary>
public sealed class JainaResilienceBuilder
{
    private readonly IServiceCollection _services;

    internal JainaResilienceBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Register a named pipeline with custom configuration. Override any of the default Jaina
    /// pipelines by re-registering it with the same name.
    /// </summary>
    public JainaResilienceBuilder AddPipeline(string name, Action<ResiliencePipelineBuilder> configure)
    {
        _services.AddResiliencePipeline(name, (builder, _) => configure(builder));
        return this;
    }

    /// <summary>
    /// Register the four default Jaina pipelines: <c>Default</c>, <c>QueuePublish</c>,
    /// <c>ExternalHttp</c>, <c>Database</c>. Already invoked by <c>AddJainaResilience()</c>.
    /// </summary>
    public JainaResilienceBuilder AddDefaultPipelines()
    {
        AddPipeline(JainaResiliencePipelines.Default, builder => builder
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .AddTimeout(TimeSpan.FromSeconds(30)));

        AddPipeline(JainaResiliencePipelines.QueuePublish, builder => builder
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxDelay = TimeSpan.FromSeconds(15),
            })
            .AddTimeout(TimeSpan.FromSeconds(60)));

        AddPipeline(JainaResiliencePipelines.ExternalHttp, builder => builder
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(300),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
            })
            .AddTimeout(TimeSpan.FromSeconds(10)));

        AddPipeline(JainaResiliencePipelines.Database, builder => builder
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(50),
                BackoffType = DelayBackoffType.Constant,
                ShouldHandle = new PredicateBuilder().Handle<TimeoutRejectedException>(),
            })
            .AddTimeout(TimeSpan.FromSeconds(5)));

        return this;
    }
}
