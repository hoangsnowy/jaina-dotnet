using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;

namespace Jaina.FeatureFlags;

/// <summary>
/// Feature filter that gates a flag on the current user (NameIdentifier or sub claim) +
/// optional role membership + percentage rollout. Pulls the principal from the active
/// <see cref="HttpContext"/> so it works for any authentication scheme (JWT, cookie, ApiKey).
/// <code>
/// "FeatureManagement": {
///   "NewDashboard": {
///     "EnabledFor": [
///       { "Name": "Jaina.User", "Parameters": {
///           "Users": [ "alice@acme", "bob@globex" ],
///           "Roles": [ "beta-tester" ],
///           "Percentage": 25
///         }
///       }
///     ]
///   }
/// }
/// </code>
/// </summary>
[FilterAlias("Jaina.User")]
public sealed class UserTargetingFilter : IFeatureFilter
{
    private readonly IHttpContextAccessor _accessor;

    public UserTargetingFilter(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        var principal = _accessor.HttpContext?.User;
        if (principal is null || principal.Identity?.IsAuthenticated != true)
            return Task.FromResult(false);

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? principal.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Task.FromResult(false);

        var settings = context.Parameters.Get<UserFilterSettings>() ?? new UserFilterSettings();

        // Explicit user allow-list
        if (settings.Users is { Length: > 0 } &&
            !settings.Users.Contains(userId, StringComparer.OrdinalIgnoreCase))
            return Task.FromResult(false);

        // Role check — any-of semantics
        if (settings.Roles is { Length: > 0 } &&
            !settings.Roles.Any(r => principal.IsInRole(r)))
            return Task.FromResult(false);

        if (settings.Percentage >= 100) return Task.FromResult(true);
        if (settings.Percentage <= 0)   return Task.FromResult(false);

        var bucket = TenantTargetingFilter.StableBucket(userId);
        return Task.FromResult(bucket < settings.Percentage);
    }
}

public sealed class UserFilterSettings
{
    public string[] Users { get; set; } = Array.Empty<string>();
    public string[] Roles { get; set; } = Array.Empty<string>();
    public int Percentage { get; set; } = 100;
}
