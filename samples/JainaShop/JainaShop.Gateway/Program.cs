using Jaina.AspNetCore;
using Jaina.HealthChecks;
using Jaina.MultiTenancy;
using Jaina.RateLimiting;
using Jaina.Resilience;
using Jaina.Samples.ServiceDefaults;
using Jaina.ServiceDiscovery;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddJainaProblemDetails();

// MultiTenancy: header → claim → host
builder.Services.AddJainaMultiTenancy(b => b
    .FromHeader("X-Tenant")
    .FromClaim("tid"));

// Rate limiting: per-tenant + per-IP defaults
builder.Services.AddJainaRateLimiting();

// Resilience: outbound HTTP
builder.Services.AddJainaResilience();

// Service discovery: resolve "catalog", "orders" from configuration
builder.Services.AddJainaServiceDiscovery();

// Typed clients to upstream services. Service discovery resolves the host;
// resilience handler retries / circuit-breaks; both layered automatically by
// the StandardResilienceHandler that ServiceDefaults wires.
builder.Services.AddHttpClient("catalog", c => c.BaseAddress = new("http://catalog"));
builder.Services.AddHttpClient("orders",  c => c.BaseAddress = new("http://orders"));

builder.Services.AddHealthChecks()
    .AddCheck("gateway-ready", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
        tags: new[] { JainaHealthCheckTags.Ready });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseJainaPipeline();
app.UseJainaTenantResolution();
app.UseRateLimiter();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapJainaHealthChecks();

// /api/products → catalog (per-IP rate-limit, no tenant required for product browsing)
app.MapGet("/api/products", async (IHttpClientFactory http, CancellationToken ct) =>
    {
        var client = http.CreateClient("catalog");
        var resp = await client.GetAsync("/products", ct);
        return Results.Stream(await resp.Content.ReadAsStreamAsync(ct), "application/json");
    })
   .RequireRateLimiting(JainaRateLimitPolicies.PerIp);

// /api/orders → orders (per-tenant rate-limit, requires tenant context)
app.MapPost("/api/orders", async (HttpRequest req, IHttpClientFactory http, ITenantContext tenants, CancellationToken ct) =>
    {
        if (!tenants.HasTenant)
            return Results.Problem(statusCode: 400, title: "Bad Request", detail: "X-Tenant header missing");

        var client = http.CreateClient("orders");
        // forward body + propagate idempotency key + tenant
        using var fwd = new HttpRequestMessage(HttpMethod.Post, "/orders");
        fwd.Content = new StreamContent(req.Body);
        if (req.Headers.TryGetValue("Content-Type", out var ct1))
            fwd.Content.Headers.TryAddWithoutValidation("Content-Type", (string?)ct1);
        if (req.Headers.TryGetValue("Idempotency-Key", out var idem))
            fwd.Headers.TryAddWithoutValidation("Idempotency-Key", (string?)idem);
        fwd.Headers.TryAddWithoutValidation("X-Tenant", tenants.Current!.TenantId);

        var resp = await client.SendAsync(fwd, ct);
        var body = await resp.Content.ReadAsByteArrayAsync(ct);
        return Results.Bytes(body,
            resp.Content.Headers.ContentType?.ToString() ?? "application/json");
    })
   .RequireRateLimiting(JainaRateLimitPolicies.PerTenant);

app.Run();
