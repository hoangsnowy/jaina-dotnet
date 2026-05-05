using Microsoft.AspNetCore.Authentication;

namespace Jaina.Security.Authentication.ApiKey;

/// <summary>
/// Configuration for the <see cref="ApiKeyAuthenticationHandler"/>. The header carrying the
/// key, plus a static dictionary of <c>key → owner identifier</c> for simple deployments.
/// Production setups typically replace the <see cref="KeyResolver"/> with a real lookup
/// (database, KeyVault, etc.).
/// </summary>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public const string DefaultHeaderName = "X-Api-Key";

    /// <summary>Header name to read the API key from. Defaults to <c>X-Api-Key</c>.</summary>
    public string HeaderName { get; set; } = DefaultHeaderName;

    /// <summary>
    /// Map of well-known keys to owner identity (typically the service or user the key
    /// represents). Use for testing or static deployments.
    /// </summary>
    public IDictionary<string, string> StaticKeys { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Plug-in resolver that looks up the owner for a given key. Returning null treats the
    /// key as unknown and the request is rejected with 401. Falls back to
    /// <see cref="StaticKeys"/> if not provided.
    /// </summary>
    public Func<string, CancellationToken, Task<string?>>? KeyResolver { get; set; }
}
