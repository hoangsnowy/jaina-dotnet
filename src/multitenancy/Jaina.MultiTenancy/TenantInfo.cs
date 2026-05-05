namespace Jaina.MultiTenancy;

/// <summary>
/// Resolved tenant identity carried through the request scope. Domain code injects
/// <see cref="ITenantContext"/> to read the current tenant; resolvers populate it.
/// </summary>
public sealed class TenantInfo
{
    /// <summary>Stable tenant identifier — used as the partition key for cache, EF, etc.</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Free-form metadata (display name, plan, region, ...) attached during resolution.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; }
        = new Dictionary<string, string>();
}
