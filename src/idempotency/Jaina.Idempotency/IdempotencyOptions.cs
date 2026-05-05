namespace Jaina.Idempotency;

/// <summary>
/// Configuration for the idempotency middleware and stores. Bind from
/// <c>appsettings.json</c> or configure in code via <c>services.Configure&lt;IdempotencyOptions&gt;</c>.
/// </summary>
public sealed class IdempotencyOptions
{
    /// <summary>
    /// HTTP header carrying the client-provided idempotency token. Defaults to <c>Idempotency-Key</c>.
    /// </summary>
    public string HeaderName { get; set; } = "Idempotency-Key";

    /// <summary>
    /// How long a successful response should be retained for replay. Defaults to 24h.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// HTTP methods that the middleware should treat as candidates for caching.
    /// Reads (GET / HEAD) are typically excluded because they're already idempotent.
    /// </summary>
    public string[] CacheableMethods { get; set; } = ["POST", "PUT", "PATCH", "DELETE"];
}
