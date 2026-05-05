using Jaina.AspNetCore;
using Jaina.HealthChecks;
using Jaina.Idempotency.AspNetCore;
using Jaina.Idempotency.InMemory;
using Jaina.Messaging.Outbox;
using Jaina.Messaging.Outbox.EfCore;
using Jaina.Observability.Telemetry;
using Jaina.Samples.ServiceDefaults;
using JainaShop.Orders;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddJainaProblemDetails();

// EF + Outbox: same DbContextFactory backs both the producer (scoped EfOutbox) and the
// relay's outbox store (singleton via factory). Use AddPooledDbContextFactory in prod.
builder.Services.AddDbContextFactory<OrdersDb>(o => o.UseInMemoryDatabase("orders"));
builder.Services.AddDbContext<OrdersDb>(o => o.UseInMemoryDatabase("orders"));
builder.Services.AddJainaEfCoreOutbox<OrdersDb>();
builder.Services.AddSingleton<IOutboxDispatcher, ConsoleOutboxDispatcher>();
builder.Services.AddJainaOutboxRelay(o =>
{
    o.PollingInterval = TimeSpan.FromMilliseconds(500);
    o.BatchSize = 25;
});

// Idempotency: HTTP middleware caches 2xx responses keyed by Idempotency-Key header
builder.Services.AddJainaInMemoryIdempotency();

builder.Services.AddHealthChecks()
    .AddCheck("orders-ready", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
        tags: new[] { JainaHealthCheckTags.Ready });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseJainaPipeline();
app.UseJainaIdempotency();   // place after auth (none in this sample) before endpoints

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<OrdersDb>();
    await db.Database.EnsureCreatedAsync();
}

app.MapJainaHealthChecks();

// ── POST /orders ───────────────────────────────────────────────────────
// Send an Idempotency-Key header to make this safely retryable. The middleware
// caches the 201 response so subsequent calls with the same key return the same
// orderId/total without re-executing the handler — no double-charge on retry.
app.MapPost("/orders", async (
    PlaceOrderRequest req,
    OrdersDb db,
    IOutbox outbox) =>
{
    using var span = JainaActivitySource.StartSpan("orders", "place");
    span?.SetTag(TagConventions.MessageType, nameof(OrderPlaced));

    var order = new Order { Sku = req.Sku, Quantity = req.Quantity, Total = req.Quantity * req.UnitPrice };
    db.Orders.Add(order);

    // Outbox write happens in the same transaction as the order — the relay picks
    // it up after SaveChanges commits. No dual-write problem.
    await outbox.EnqueueAsync(
        new OrderPlaced(order.Id, order.Sku, order.Quantity, order.Total),
        destination: "orders.events",
        headers: new Dictionary<string, string> { ["correlation-id"] = System.Diagnostics.Activity.Current?.Id ?? "n/a" });

    await db.SaveChangesAsync();

    span?.SetTag(TagConventions.MessageId, order.Id.ToString());
    return Results.Created($"/orders/{order.Id}", order);
});

app.MapGet("/orders/{id:guid}", async (Guid id, OrdersDb db) =>
{
    var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

// Inspect the outbox state — useful for the demo, drop in production
app.MapGet("/_outbox", async (OrdersDb db) =>
{
    var msgs = await db.Set<OutboxMessage>()
        .AsNoTracking()
        .OrderByDescending(m => m.CreatedAt)
        .Take(50)
        .Select(m => new { m.Id, m.PayloadType, m.Destination, m.Attempts, m.ProcessedAt, m.LastError })
        .ToArrayAsync();
    return Results.Ok(msgs);
});

app.Run();

public record PlaceOrderRequest(string Sku, int Quantity, decimal UnitPrice);
