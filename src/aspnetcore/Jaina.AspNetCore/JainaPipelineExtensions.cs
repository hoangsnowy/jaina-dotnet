using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Jaina.AspNetCore;

/// <summary>
/// Opinionated middleware composer that wires the Jaina pipeline in the recommended order:
/// <list type="number">
///   <item><c>UseExceptionHandler</c> + <c>UseStatusCodePages</c> (ProblemDetails)</item>
///   <item>Correlation ID — header in/out + log scope (deferred to user-supplied middleware)</item>
///   <item>Authentication (caller registers <c>UseAuthentication</c>)</item>
///   <item>Authorization (caller registers <c>UseAuthorization</c>)</item>
///   <item>Tenant resolution — <c>UseJainaTenantResolution</c> (Jaina.MultiTenancy package)</item>
///   <item>Idempotency replay — <c>UseJainaIdempotency</c> (Jaina.Idempotency.AspNetCore)</item>
///   <item>Rate limiting — <c>UseRateLimiter</c> (caller registers)</item>
/// </list>
/// This composer wires the pieces this package owns (exception handling + status codes).
/// The other steps live in their own packages and the caller still wires them explicitly,
/// so the dependency graph stays minimal.
/// </summary>
public static class JainaPipelineExtensions
{
    /// <summary>
    /// Apply the Jaina-owned middleware in the recommended order: <c>UseExceptionHandler</c>
    /// followed by <c>UseStatusCodePages</c>. Returns the builder so callers can chain
    /// their own additions (auth, tenant, idempotency, rate-limit, endpoints).
    /// </summary>
    /// <example>
    /// <code>
    /// app.UseJainaPipeline()
    ///    .UseAuthentication()
    ///    .UseAuthorization()
    ///    .UseJainaTenantResolution()    // Jaina.MultiTenancy
    ///    .UseJainaIdempotency()         // Jaina.Idempotency.AspNetCore
    ///    .UseRateLimiter();             // System.Threading.RateLimiting
    /// </code>
    /// </example>
    public static IApplicationBuilder UseJainaPipeline(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
