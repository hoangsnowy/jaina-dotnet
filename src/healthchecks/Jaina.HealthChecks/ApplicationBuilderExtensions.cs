using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Jaina.HealthChecks;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Map the standard Jaina health endpoints:
    /// <list type="bullet">
    ///   <item><c>/health/live</c> — only checks tagged <c>"live"</c> (process responsiveness)</item>
    ///   <item><c>/health/ready</c> — only checks tagged <c>"ready"</c> (downstream dependencies)</item>
    /// </list>
    /// Kubernetes liveness / readiness probes can hit them directly. Register checks with the
    /// matching tag via <see cref="JainaHealthCheckTags"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapJainaHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(JainaHealthCheckTags.Live),
        });

        endpoints.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(JainaHealthCheckTags.Ready),
        });

        return endpoints;
    }
}
