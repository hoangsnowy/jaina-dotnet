using System.Security.Claims;

namespace Jaina.Security.Authentication.UserContext;

/// <summary>
/// Per-request snapshot of the authenticated principal. Inject this anywhere domain code
/// needs to know "who is calling?" — handlers, repositories, audit logging. Hides the
/// raw HttpContext dependency from the inner layers.
/// </summary>
public interface IUserContext
{
    /// <summary>True if a user has authenticated (any scheme).</summary>
    bool IsAuthenticated { get; }

    /// <summary>Stable user identifier (NameIdentifier claim by default). Null when anonymous.</summary>
    string? UserId { get; }

    /// <summary>Display name (Name claim). Null when anonymous.</summary>
    string? UserName { get; }

    /// <summary>Underlying <see cref="ClaimsPrincipal"/> for advanced claim access.</summary>
    ClaimsPrincipal? Principal { get; }

    /// <summary>True if the user has the given OAuth/JWT scope value.</summary>
    bool HasScope(string scope);

    /// <summary>True if the principal carries the given role.</summary>
    bool IsInRole(string role);

    /// <summary>Get a single claim value by type, or null if not present.</summary>
    string? GetClaim(string claimType);
}
