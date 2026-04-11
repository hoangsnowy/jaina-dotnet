using Jaina.Caching;
using Jaina.Caching.Memory;
using Jaina.Core.Results;
using Jaina.Data.Cqrs;
using Jaina.Data.Cqrs.Commands;
using Jaina.Data.Cqrs.Queries;
using Jaina.Notifications;
using Jaina.Notifications.Sms;
using Jaina.Samples.ServiceDefaults;
using Jaina.Security.Encryption;
using Jaina.Security.Hashing;
using Jaina.Storage;
using Jaina.Storage.Local;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults();
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

var app = builder.Build();

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
    return value is not null ? Results.Ok(Result.Ok(value)) : Results.NotFound();
});

app.MapPost("/api/cache/{key}", (string key, string value, ICache cache) =>
{
    cache.Set(key, value, TimeSpan.FromMinutes(5));
    return Results.Ok(Result.Ok("Cached"));
});

app.MapDelete("/api/cache/{key}", (string key, ICache cache) =>
{
    cache.Remove(key);
    return Results.Ok(Result.Ok("Removed"));
});

// ── Storage ────────────────────────────────────────────────────────────
app.MapPost("/api/files/{*path}", async (string path, IFileStorage storage) =>
{
    var content = System.Text.Encoding.UTF8.GetBytes($"Sample content created at {DateTime.UtcNow:O}");
    await storage.SaveAsync(path, content);
    return Results.Ok(Result.Ok("File saved"));
});

app.MapGet("/api/files/{*path}", async (string path, IFileStorage storage) =>
{
    if (!await storage.ExistsAsync(path))
        return Results.NotFound();
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
    return Results.Ok(Result.Ok("Item created"));
});

app.MapGet("/api/items/{id:int}", async (int id, IQueryBus bus) =>
{
    var item = await bus.SendAsync<GetItemQuery, ItemDto?>(new GetItemQuery(id));
    return item is not null ? Results.Ok(Result.Ok(item)) : Results.NotFound();
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
    return Results.Ok(Result.Ok("SMS queued (logged to console in dev mode)"));
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
