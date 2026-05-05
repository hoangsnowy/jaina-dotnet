using Microsoft.AspNetCore.Authorization;

namespace Jaina.Security.Authentication.Policies;

public static class ScopePolicyExtensions
{
    /// <summary>
    /// Require the principal to carry the given OAuth scope value (in either the
    /// <c>scope</c> or <c>scp</c> claim, space-separated per RFC 8693). Adds the requirement
    /// to the policy builder; chain with other requirements as needed.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddAuthorizationBuilder()
    ///     .AddPolicy("orders:read",  p => p.RequireJainaScope("orders.read"))
    ///     .AddPolicy("orders:write", p => p.RequireJainaScope("orders.write"));
    /// </code>
    /// </example>
    public static AuthorizationPolicyBuilder RequireJainaScope(this AuthorizationPolicyBuilder builder, string scope)
    {
        builder.RequireAssertion(ctx =>
        {
            var raw = ctx.User.FindFirst("scope")?.Value
                   ?? ctx.User.FindFirst("scp")?.Value;
            if (string.IsNullOrEmpty(raw)) return false;
            return raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Contains(scope, StringComparer.Ordinal);
        });
        return builder;
    }

    /// <summary>
    /// Require the principal was authenticated via the API key scheme (i.e. carries a
    /// <c>auth_method = api-key</c> claim).
    /// </summary>
    public static AuthorizationPolicyBuilder RequireJainaApiKey(this AuthorizationPolicyBuilder builder) =>
        builder.RequireClaim("auth_method", "api-key");
}
