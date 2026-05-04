namespace Jaina.Resilience;

/// <summary>
/// Well-known names of the resilience pipelines that <c>AddJainaResilience()</c> registers
/// by default. Resolve a pipeline by name via <c>ResiliencePipelineProvider&lt;string&gt;</c>.
/// </summary>
public static class JainaResiliencePipelines
{
    /// <summary>General-purpose pipeline — retry with exponential backoff + 30s timeout.</summary>
    public const string Default = "jaina.default";

    /// <summary>For publishing to a message broker — generous retries, 60s timeout.</summary>
    public const string QueuePublish = "jaina.queue-publish";

    /// <summary>For outbound HTTP calls — retry + circuit breaker + 10s timeout.</summary>
    public const string ExternalHttp = "jaina.external-http";

    /// <summary>For database operations — fast retries on transient errors only.</summary>
    public const string Database = "jaina.database";
}
