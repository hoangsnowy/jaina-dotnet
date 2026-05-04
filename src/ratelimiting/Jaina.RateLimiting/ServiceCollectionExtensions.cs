using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Jaina.RateLimiting;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register four opinionated rate-limit policies under the names in
    /// <see cref="JainaRateLimitPolicies"/>. Apply on endpoints with
    /// <c>.RequireRateLimiting(JainaRateLimitPolicies.PerTenant)</c> after
    /// <c>app.UseRateLimiter()</c>.
    /// </summary>
    public static IServiceCollection AddJainaRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(JainaRateLimitPolicies.PerIp, ctx =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 100,
                        TokensPerPeriod = 100,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            options.AddPolicy(JainaRateLimitPolicies.PerUser, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ctx.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 600,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.AddPolicy(JainaRateLimitPolicies.PerTenant, ctx =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: ctx.Request.Headers["X-Tenant"].FirstOrDefault() ?? "anon",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 1000,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                    }));

            options.AddPolicy(JainaRateLimitPolicies.Concurrency, _ =>
                RateLimitPartition.GetConcurrencyLimiter(
                    partitionKey: "global",
                    factory: _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = 10,
                        QueueLimit = 0,
                    }));
        });
        return services;
    }
}
