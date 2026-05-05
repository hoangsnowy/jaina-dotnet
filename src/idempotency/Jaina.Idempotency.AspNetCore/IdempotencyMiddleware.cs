using Jaina.Observability.Telemetry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Jaina.Idempotency.AspNetCore;

/// <summary>
/// Reads the configured idempotency header from incoming requests; on a cache hit replays the
/// previously stored response, on a miss buffers the response so successful results can be cached.
/// Only requests whose method is in <see cref="IdempotencyOptions.CacheableMethods"/> are
/// considered.
/// </summary>
public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IIdempotencyStore _store;
    private readonly IdempotencyOptions _opts;

    public IdempotencyMiddleware(RequestDelegate next, IIdempotencyStore store, IOptions<IdempotencyOptions> opts)
    {
        _next = next;
        _store = store;
        _opts = opts.Value;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!IsCacheable(ctx) ||
            !ctx.Request.Headers.TryGetValue(_opts.HeaderName, out var keys) ||
            keys.Count == 0 ||
            string.IsNullOrWhiteSpace(keys[0]))
        {
            await _next(ctx);
            return;
        }

        var key = keys[0]!;
        using var span = JainaActivitySource.StartSpan("idempotency", "evaluate");
        span?.SetTag(TagConventions.IdempotencyKey, key);

        var existing = await _store.GetAsync(key, ctx.RequestAborted);
        if (existing is not null)
        {
            span?.SetTag(TagConventions.IdempotencyReplay, true);
            await ReplayAsync(ctx, existing);
            return;
        }

        span?.SetTag(TagConventions.IdempotencyReplay, false);
        await CaptureAndStoreAsync(ctx, key);
    }

    private bool IsCacheable(HttpContext ctx) =>
        Array.Exists(_opts.CacheableMethods, m => string.Equals(m, ctx.Request.Method, StringComparison.OrdinalIgnoreCase));

    private static async Task ReplayAsync(HttpContext ctx, IdempotencyEntry entry)
    {
        ctx.Response.StatusCode = entry.StatusCode;
        if (!string.IsNullOrEmpty(entry.ContentType))
            ctx.Response.ContentType = entry.ContentType;
        ctx.Response.Headers["Idempotent-Replay"] = "true";
        await ctx.Response.Body.WriteAsync(entry.Body, ctx.RequestAborted);
    }

    private async Task CaptureAndStoreAsync(HttpContext ctx, string key)
    {
        var original = ctx.Response.Body;
        using var capture = new MemoryStream();
        ctx.Response.Body = capture;

        try
        {
            await _next(ctx);

            capture.Seek(0, SeekOrigin.Begin);
            await capture.CopyToAsync(original, ctx.RequestAborted);

            if (ctx.Response.StatusCode is >= 200 and < 300)
            {
                var entry = new IdempotencyEntry(
                    ctx.Response.StatusCode,
                    ctx.Response.ContentType,
                    capture.ToArray(),
                    DateTimeOffset.UtcNow);
                await _store.SetAsync(key, entry, _opts.DefaultTtl, ctx.RequestAborted);
            }
        }
        finally
        {
            ctx.Response.Body = original;
        }
    }
}
