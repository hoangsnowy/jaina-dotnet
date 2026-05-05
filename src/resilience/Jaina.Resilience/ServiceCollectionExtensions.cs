using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Resilience;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the four default Jaina resilience pipelines (<c>JainaResiliencePipelines.*</c>).
    /// Pass a configure action to add or override pipelines.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddJainaResilience(b => b.AddPipeline("custom", p => p.AddRetry(...)));
    /// </code>
    /// </example>
    public static IServiceCollection AddJainaResilience(
        this IServiceCollection services,
        Action<JainaResilienceBuilder>? configure = null)
    {
        var builder = new JainaResilienceBuilder(services);
        builder.AddDefaultPipelines();
        configure?.Invoke(builder);
        return services;
    }
}
