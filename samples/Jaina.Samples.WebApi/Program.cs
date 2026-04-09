using Jaina.Caching;
using Jaina.Caching.Memory;
using Jaina.Core.Results;
using Jaina.Samples.ServiceDefaults;
using Jaina.Storage;
using Jaina.Storage.Local;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults();
builder.Services.AddJainaMemoryCache();
builder.Services.AddJainaLocalStorage(o => o.BasePath = Path.Combine(Path.GetTempPath(), "jaina-samples"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints();

// Cache demo endpoints
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

// Storage demo endpoints
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

app.Run();
