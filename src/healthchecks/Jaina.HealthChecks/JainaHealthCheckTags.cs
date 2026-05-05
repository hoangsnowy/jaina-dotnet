namespace Jaina.HealthChecks;

/// <summary>
/// Standard tags so <c>/health/live</c> and <c>/health/ready</c> endpoints can filter the
/// right subset. Apply when registering: <c>builder.AddCheck("redis", ..., tags: new[] { JainaHealthCheckTags.Ready })</c>.
/// </summary>
public static class JainaHealthCheckTags
{
    /// <summary>"alive" — process is up; checks that don't touch external dependencies.</summary>
    public const string Live = "live";

    /// <summary>"ready" — process is ready to serve traffic; depends on external services.</summary>
    public const string Ready = "ready";
}
