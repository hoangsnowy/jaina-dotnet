using Jaina.AspNetCore;
using Jaina.Caching;
using Jaina.Caching.Memory;
using Jaina.HealthChecks;
using Jaina.Observability.Telemetry;
using Jaina.Samples.ServiceDefaults;
using JainaShop.Catalog;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddJainaProblemDetails();
builder.Services.AddJainaMemoryCache();

// EF Core in-memory for the sample. Swap for Postgres in production.
builder.Services.AddDbContext<CatalogDb>(o => o.UseInMemoryDatabase("catalog"));

// Health: live tag added by ServiceDefaults; mark this service ready when boot completed
builder.Services.AddHealthChecks()
    .AddCheck("catalog-ready", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
        tags: new[] { JainaHealthCheckTags.Ready });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseJainaPipeline();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Seed a few products on first run
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDb>();
    if (!await db.Products.AnyAsync())
    {
        db.Products.AddRange(
            new Product { Sku = "WIDGET-001", Name = "Standard Widget", Price = 9.99m,  Stock = 200 },
            new Product { Sku = "GADGET-002", Name = "Premium Gadget",  Price = 29.99m, Stock = 50  },
            new Product { Sku = "DEVICE-003", Name = "Pro Device",      Price = 99.99m, Stock = 12  });
        await db.SaveChangesAsync();
    }
}

app.MapJainaHealthChecks();

// ── Endpoints ──────────────────────────────────────────────────────────

app.MapGet("/products", async (CatalogDb db, ICache cache) =>
{
    using var span = JainaActivitySource.StartSpan("catalog", "products.list");

    const string key = "catalog:products:all";
    var cached = await cache.GetAsync<Product[]>(key);
    if (cached is not null)
    {
        span?.SetTag(TagConventions.CacheHit, true);
        return Results.Ok(cached);
    }

    span?.SetTag(TagConventions.CacheHit, false);
    var items = await db.Products.AsNoTracking().ToArrayAsync();
    await cache.SetAsync(key, items, TimeSpan.FromSeconds(30));
    return Results.Ok(items);
});

app.MapGet("/products/{id:guid}", async (Guid id, CatalogDb db, ICache cache) =>
{
    using var span = JainaActivitySource.StartSpan("catalog", "products.get");
    span?.SetTag(TagConventions.CacheKey, id.ToString());

    var key = $"catalog:product:{id}";
    var cached = await cache.GetAsync<Product>(key);
    if (cached is not null)
    {
        span?.SetTag(TagConventions.CacheHit, true);
        return Results.Ok(cached);
    }

    span?.SetTag(TagConventions.CacheHit, false);
    var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    if (product is null) return Results.NotFound();

    await cache.SetAsync(key, product, TimeSpan.FromMinutes(5));
    return Results.Ok(product);
});

app.MapPost("/products", async (CreateProductRequest req, CatalogDb db, ICache cache) =>
{
    using var span = JainaActivitySource.StartSpan("catalog", "products.create");

    var product = new Product { Sku = req.Sku, Name = req.Name, Price = req.Price, Stock = req.Stock };
    db.Products.Add(product);
    await db.SaveChangesAsync();

    // Invalidate the list cache; per-id cache populates on first read
    cache.Remove("catalog:products:all");

    return Results.Created($"/products/{product.Id}", product);
});

app.Run();

public record CreateProductRequest(string Sku, string Name, decimal Price, int Stock);
