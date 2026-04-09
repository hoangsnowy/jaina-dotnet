namespace Jaina.Caching;

public class CacheEntryOptions
{
    public TimeSpan? SlidingExpiration { get; set; }
    public DateTimeOffset? AbsoluteExpiration { get; set; }
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

    public static CacheEntryOptions Sliding(TimeSpan expiry) => new() { SlidingExpiration = expiry };
    public static CacheEntryOptions Absolute(DateTimeOffset expiry) => new() { AbsoluteExpiration = expiry };
    public static CacheEntryOptions AbsoluteRelative(TimeSpan expiry) => new() { AbsoluteExpirationRelativeToNow = expiry };
}
