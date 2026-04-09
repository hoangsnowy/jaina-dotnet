namespace Jaina.Caching;

public interface IDistributedLock
{
    Task<bool> AcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default);
    Task<bool> ReleaseAsync(string key, CancellationToken ct = default);
}
