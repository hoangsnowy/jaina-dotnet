namespace Jaina.RateLimiting;

/// <summary>
/// Well-known names of policies registered by <see cref="ServiceCollectionExtensions.AddJainaRateLimiting"/>.
/// Reference these on endpoints with <c>.RequireRateLimiting(JainaRateLimitPolicies.PerIp)</c>.
/// </summary>
public static class JainaRateLimitPolicies
{
    /// <summary>Per remote IP — token bucket, 100 req / minute, burst 20.</summary>
    public const string PerIp = "jaina.per-ip";

    /// <summary>Per authenticated user (NameIdentifier claim) — fixed window, 600 req / minute.</summary>
    public const string PerUser = "jaina.per-user";

    /// <summary>Per tenant (X-Tenant header) — sliding window, 1000 req / minute.</summary>
    public const string PerTenant = "jaina.per-tenant";

    /// <summary>Concurrency cap on a route — at most 10 in-flight at once.</summary>
    public const string Concurrency = "jaina.concurrency";
}
