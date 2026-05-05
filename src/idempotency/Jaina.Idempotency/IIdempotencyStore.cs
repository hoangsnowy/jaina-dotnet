namespace Jaina.Idempotency;

/// <summary>
/// Stores the result of a successful operation keyed by an idempotency token, so that a
/// repeated request with the same key returns the previous result instead of executing again.
/// Implementations must be safe for concurrent access from multiple request threads.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Look up a previously stored entry. Returns null when no entry exists or it has expired.
    /// </summary>
    Task<IdempotencyEntry?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Store an entry for the given TTL. Existing entries with the same key are overwritten.
    /// </summary>
    Task SetAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken ct = default);
}
