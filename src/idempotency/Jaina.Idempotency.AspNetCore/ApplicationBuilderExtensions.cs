using Microsoft.AspNetCore.Builder;

namespace Jaina.Idempotency.AspNetCore;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Insert the idempotency middleware into the pipeline. Place after authentication but
    /// before endpoint execution. Requires an <see cref="IIdempotencyStore"/> registration —
    /// e.g. <c>services.AddJainaInMemoryIdempotency()</c> or a Redis provider.
    /// </summary>
    public static IApplicationBuilder UseJainaIdempotency(this IApplicationBuilder app) =>
        app.UseMiddleware<IdempotencyMiddleware>();
}
