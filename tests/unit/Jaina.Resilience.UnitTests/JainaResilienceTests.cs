using Jaina.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Polly.Retry;

namespace Jaina.Resilience.UnitTests;

public class JainaResilienceTests
{
    [Fact]
    public void AddJainaResilience_RegistersAllDefaultPipelines()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJainaResilience();
        var provider = services.BuildServiceProvider();
        var pipelines = provider.GetRequiredService<ResiliencePipelineProvider<string>>();

        // Assert — each named pipeline resolves
        Assert.NotNull(pipelines.GetPipeline(JainaResiliencePipelines.Default));
        Assert.NotNull(pipelines.GetPipeline(JainaResiliencePipelines.QueuePublish));
        Assert.NotNull(pipelines.GetPipeline(JainaResiliencePipelines.ExternalHttp));
        Assert.NotNull(pipelines.GetPipeline(JainaResiliencePipelines.Database));
    }

    [Fact]
    public async Task DefaultPipeline_RetriesOnTransientException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddJainaResilience();
        var pipelines = services.BuildServiceProvider().GetRequiredService<ResiliencePipelineProvider<string>>();
        var pipeline = pipelines.GetPipeline(JainaResiliencePipelines.Default);

        var attempts = 0;

        // Act
        await pipeline.ExecuteAsync(async ct =>
        {
            attempts++;
            if (attempts < 2) throw new InvalidOperationException("transient");
            await Task.CompletedTask;
        });

        // Assert — first attempt failed, second succeeded → 2 total
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void AddJainaResilience_AllowsCustomPipelineRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddJainaResilience(b => b.AddPipeline("custom", p => p
            .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 1 })));

        var pipelines = services.BuildServiceProvider().GetRequiredService<ResiliencePipelineProvider<string>>();

        // Assert
        Assert.NotNull(pipelines.GetPipeline("custom"));
    }

    [Fact]
    public void AddJainaResilience_AllowsOverrideOfDefaultPipelineByName()
    {
        // Arrange / Act — re-register the Default pipeline with a stricter config
        var services = new ServiceCollection();
        services.AddJainaResilience(b => b.AddPipeline(JainaResiliencePipelines.Default, p => p
            .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 0 })));

        var pipelines = services.BuildServiceProvider().GetRequiredService<ResiliencePipelineProvider<string>>();

        // Assert — overridden pipeline still resolves
        Assert.NotNull(pipelines.GetPipeline(JainaResiliencePipelines.Default));
    }
}
