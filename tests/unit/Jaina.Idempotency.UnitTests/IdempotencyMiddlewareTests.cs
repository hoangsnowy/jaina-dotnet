using System.Text;
using Jaina.Idempotency;
using Jaina.Idempotency.AspNetCore;
using Jaina.Idempotency.InMemory;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Jaina.Idempotency.UnitTests;

public class IdempotencyMiddlewareTests
{
    private readonly IIdempotencyStore _store = new InMemoryIdempotencyStore(new MemoryCache(new MemoryCacheOptions()));
    private readonly IOptions<IdempotencyOptions> _opts = Options.Create(new IdempotencyOptions());

    [Fact]
    public async Task Get_WithoutHeader_PassesThrough_NoCaching()
    {
        // Arrange
        var executions = 0;
        var middleware = new IdempotencyMiddleware(
            next: ctx => { executions++; ctx.Response.StatusCode = 200; return Task.CompletedTask; },
            store: _store, opts: _opts);

        // Act — call twice, no idempotency header, both execute
        await middleware.InvokeAsync(MakeContext("GET"));
        await middleware.InvokeAsync(MakeContext("GET"));

        // Assert
        Assert.Equal(2, executions);
    }

    [Fact]
    public async Task Post_WithKey_FirstCall_Executes_SecondCall_Replays()
    {
        // Arrange
        var executions = 0;
        var middleware = new IdempotencyMiddleware(
            next: async ctx =>
            {
                executions++;
                ctx.Response.StatusCode = 201;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("{\"id\":42}");
            },
            store: _store, opts: _opts);

        // Act
        var first = MakeContext("POST", idempotencyKey: "abc");
        await middleware.InvokeAsync(first);

        var second = MakeContext("POST", idempotencyKey: "abc");
        await middleware.InvokeAsync(second);

        // Assert — handler ran once, second call returned cached response with replay marker
        Assert.Equal(1, executions);
        Assert.Equal(201, second.Response.StatusCode);
        Assert.Equal("true", second.Response.Headers["Idempotent-Replay"].ToString());
        Assert.Equal("{\"id\":42}", ReadBody(second));
    }

    [Fact]
    public async Task Post_WithKey_FailedResponse_NotCached()
    {
        // Arrange — handler returns 500 the first time, 200 the second
        var executions = 0;
        var middleware = new IdempotencyMiddleware(
            next: ctx =>
            {
                executions++;
                ctx.Response.StatusCode = executions == 1 ? 500 : 200;
                return Task.CompletedTask;
            },
            store: _store, opts: _opts);

        // Act
        await middleware.InvokeAsync(MakeContext("POST", idempotencyKey: "fails"));
        var second = MakeContext("POST", idempotencyKey: "fails");
        await middleware.InvokeAsync(second);

        // Assert — handler ran twice (failure was not cached)
        Assert.Equal(2, executions);
        Assert.Equal(200, second.Response.StatusCode);
    }

    [Fact]
    public async Task Get_WithKey_NotCached_GetIsExcludedByDefault()
    {
        // Arrange
        var executions = 0;
        var middleware = new IdempotencyMiddleware(
            next: ctx => { executions++; ctx.Response.StatusCode = 200; return Task.CompletedTask; },
            store: _store, opts: _opts);

        // Act — GET with header, twice
        await middleware.InvokeAsync(MakeContext("GET", idempotencyKey: "ignored"));
        await middleware.InvokeAsync(MakeContext("GET", idempotencyKey: "ignored"));

        // Assert
        Assert.Equal(2, executions);
    }

    private static HttpContext MakeContext(string method, string? idempotencyKey = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Response.Body = new MemoryStream();
        if (idempotencyKey is not null)
            ctx.Request.Headers["Idempotency-Key"] = idempotencyKey;
        return ctx;
    }

    private static string ReadBody(HttpContext ctx)
    {
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        return Encoding.UTF8.GetString(((MemoryStream)ctx.Response.Body).ToArray());
    }
}
