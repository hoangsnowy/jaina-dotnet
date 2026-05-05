using System.Diagnostics;
using Jaina.AspNetCore;
using Jaina.Caching;
using Jaina.Caching.Memory;
using Jaina.Data.Cqrs;
using Jaina.Data.Cqrs.Commands;
using Jaina.Data.Cqrs.Queries;
using Jaina.Idempotency.AspNetCore;
using Jaina.Idempotency.InMemory;
using Jaina.Messaging.Outbox;
using Jaina.Messaging.Outbox.InMemory;
using Jaina.Notifications.ConsoleSms;
using Jaina.Notifications.Sms;
using Jaina.Resilience;
using Jaina.Samples.ServiceDefaults;
using Jaina.Security.Encryption;
using Jaina.Security.Hashing;
using Jaina.Storage;
using Jaina.Storage.Local;
using Polly.Registry;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddJainaProblemDetails();
builder.Services.AddJainaMemoryCache();
builder.Services.AddJainaLocalStorage(o => o.BasePath = Path.Combine(Path.GetTempPath(), "jaina-samples"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CQRS
builder.Services.AddJainaCqrs();
builder.Services.AddCommandHandler<CreateItemCommand, CreateItemCommandHandler>();
builder.Services.AddQueryHandler<GetItemQuery, ItemDto?, GetItemQueryHandler>();

// Notifications (console SMS for dev)
builder.Services.AddJainaConsoleSms();

// M1 microservice spine demos ---------------------------------------------
builder.Services.AddJainaResilience();
builder.Services.AddJainaInMemoryIdempotency();
builder.Services.AddJainaInMemoryOutbox();
builder.Services.AddSingleton<IOutboxDispatcher, ConsoleOutboxDispatcher>();
builder.Services.AddJainaOutboxRelay(o =>
{
    o.PollingInterval = TimeSpan.FromMilliseconds(500);
    o.BatchSize = 25;
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseJainaIdempotency();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints();

// ── Cache ──────────────────────────────────────────────────────────────
app.MapGet("/api/cache/{key}", (string key, ICache cache) =>
{
    var value = cache.Get<string>(key);
    return value is not null
        ? Results.Ok(value)
        : Results.Problem(title: "Not Found", detail: $"Cache key '{key}' does not exist.", statusCode: 404);
});

app.MapPost("/api/cache/{key}", (string key, string value, ICache cache) =>
{
    cache.Set(key, value, TimeSpan.FromMinutes(5));
    return Results.Ok(new { message = "Cached" });
});

app.MapDelete("/api/cache/{key}", (string key, ICache cache) =>
{
    cache.Remove(key);
    return Results.Ok(new { message = "Removed" });
});

// ── Storage ────────────────────────────────────────────────────────────
app.MapPost("/api/files/{*path}", async (string path, IFileStorage storage) =>
{
    var content = System.Text.Encoding.UTF8.GetBytes($"Sample content created at {DateTime.UtcNow:O}");
    await storage.SaveAsync(path, content);
    return Results.Ok(new { message = "File saved" });
});

app.MapGet("/api/files/{*path}", async (string path, IFileStorage storage) =>
{
    if (!await storage.ExistsAsync(path))
        return Results.Problem(title: "Not Found", detail: $"File '{path}' does not exist.", statusCode: 404);
    var bytes = await storage.GetBytesAsync(path);
    return Results.Text(System.Text.Encoding.UTF8.GetString(bytes));
});

app.MapGet("/api/files", async (string? directory, IFileStorage storage) =>
{
    var files = await storage.GetFileNamesAsync(directory ?? "");
    return Results.Ok(files);
});

// ── CQRS ───────────────────────────────────────────────────────────────
app.MapPost("/api/items", async (CreateItemRequest req, ICommandBus bus) =>
{
    await bus.SendAsync(new CreateItemCommand(req.Name));
    return Results.Ok(new { message = "Item created" });
});

app.MapGet("/api/items/{id:int}", async (int id, IQueryBus bus) =>
{
    var item = await bus.SendAsync<GetItemQuery, ItemDto?>(new GetItemQuery(id));
    return item is not null
        ? Results.Ok(item)
        : Results.Problem(title: "Not Found", detail: $"Item {id} not found.", statusCode: 404);
});

// ── Security ───────────────────────────────────────────────────────────
app.MapPost("/api/security/hash", (HashRequest req) =>
{
    var hash = BcryptHelper.Hash(req.Password);
    return Results.Ok(new { hash });
});

app.MapPost("/api/security/verify", (VerifyRequest req) =>
{
    var valid = BcryptHelper.Verify(req.Password, req.Hash);
    return Results.Ok(new { valid });
});

app.MapPost("/api/security/encrypt", (EncryptRequest req) =>
{
    var cipher = AesHelper.Encrypt(req.PlainText, req.Pepper, req.Salt);
    return Results.Ok(new { cipher });
});

app.MapPost("/api/security/decrypt", (DecryptRequest req) =>
{
    var plain = AesHelper.Decrypt(req.CipherText, req.Pepper, req.Salt);
    return Results.Ok(new { plain });
});

// ── Notifications ──────────────────────────────────────────────────────
app.MapPost("/api/notify/sms", async (SmsRequest req, ISmsSender sms) =>
{
    await sms.SendAsync(new SmsMessage { From = req.From, To = req.To, Body = req.Body });
    return Results.Ok(new { message = "SMS queued (logged to console in dev mode)" });
});

// ── Resilience ─────────────────────────────────────────────────────────
// Demonstrates the "external-http" pipeline catching transient failures.
// Pass ?fail=true to simulate the first attempt throwing — pipeline retries.
app.MapGet("/api/resilience/flaky", async (bool? fail, ResiliencePipelineProvider<string> pipelines) =>
{
    var pipeline = pipelines.GetPipeline(JainaResiliencePipelines.ExternalHttp);
    var attempts = 0;
    var result = await pipeline.ExecuteAsync(_ =>
    {
        attempts++;
        if (fail == true && attempts < 2)
            throw new HttpRequestException("simulated transient failure");
        return ValueTask.FromResult(new { attempts, ok = true });
    });
    return Results.Ok(result);
});

// ── Idempotency ────────────────────────────────────────────────────────
// POST with header `Idempotency-Key: <key>`. The middleware caches the 2xx response;
// subsequent calls with the same key replay it (200/201 + Idempotent-Replay: true).
app.MapPost("/api/orders", (OrderRequest req) =>
{
    var orderId = Guid.NewGuid();
    return Results.Created($"/api/orders/{orderId}", new { orderId, req.Sku, req.Quantity, createdAt = DateTimeOffset.UtcNow });
});

// ── Outbox ─────────────────────────────────────────────────────────────
// Enqueue a domain event into the outbox. The relay dispatches it asynchronously
// (here just logs to console). In production the dispatcher publishes to RabbitMQ etc.
app.MapPost("/api/outbox/order-placed", async (OrderRequest req, IOutbox outbox) =>
{
    await outbox.EnqueueAsync(
        new OrderPlacedEvent(Guid.NewGuid(), req.Sku, req.Quantity),
        destination: "orders.events",
        headers: new Dictionary<string, string> { ["correlation-id"] = Activity.Current?.Id ?? "n/a" });
    return Results.Accepted(value: new { message = "Event enqueued; relay will dispatch shortly" });
});

app.MapGet("/api/outbox/snapshot", (InMemoryOutboxStore store) =>
{
    var msgs = store.Snapshot()
        .Select(m => new { m.Id, m.PayloadType, m.Destination, m.Attempts, m.ProcessedAt, m.LastError })
        .ToArray();
    return Results.Ok(msgs);
});

app.Run();

// ── CQRS types ─────────────────────────────────────────────────────────

record CreateItemRequest(string Name);
record CreateItemCommand(string Name) : ICommand;
record GetItemQuery(int Id) : IQuery<ItemDto?>;
record ItemDto(int Id, string Name);

// In-memory store shared between handlers
static class ItemStore
{
    private static int _nextId = 1;
    private static readonly Dictionary<int, ItemDto> _items = new();

    public static ItemDto Add(string name)
    {
        var item = new ItemDto(_nextId++, name);
        _items[item.Id] = item;
        return item;
    }

    public static ItemDto? Get(int id) =>
        _items.TryGetValue(id, out var item) ? item : null;
}

class CreateItemCommandHandler : ICommandHandler<CreateItemCommand>
{
    public Task HandleAsync(CreateItemCommand cmd, CancellationToken ct = default)
    {
        ItemStore.Add(cmd.Name);
        return Task.CompletedTask;
    }
}

class GetItemQueryHandler : IQueryHandler<GetItemQuery, ItemDto?>
{
    public Task<ItemDto?> HandleAsync(GetItemQuery query, CancellationToken ct = default) =>
        Task.FromResult(ItemStore.Get(query.Id));
}

// ── Request DTOs ───────────────────────────────────────────────────────
record HashRequest(string Password);
record VerifyRequest(string Password, string Hash);
record EncryptRequest(string PlainText, string Pepper, string Salt);
record DecryptRequest(string CipherText, string Pepper, string Salt);
record SmsRequest(string From, string To, string Body);
record OrderRequest(string Sku, int Quantity);
record OrderPlacedEvent(Guid OrderId, string Sku, int Quantity);

// Sample dispatcher — production code would publish to RabbitMQ/ServiceBus/Kafka.
sealed class ConsoleOutboxDispatcher : IOutboxDispatcher
{
    private readonly ILogger<ConsoleOutboxDispatcher> _logger;
    public ConsoleOutboxDispatcher(ILogger<ConsoleOutboxDispatcher> logger) => _logger = logger;

    public Task DispatchAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[outbox] dispatch {Id} type={Type} dest={Destination} payload={Payload}",
            message.Id, message.PayloadType, message.Destination, message.Payload);
        return Task.CompletedTask;
    }
}
