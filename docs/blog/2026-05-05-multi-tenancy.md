---
title: "Multi-tenant SaaS: separate tenants without separate databases"
date: 2026-05-05
tags: [multitenancy, saas, ef-core, microservices]
reading_time: "~8 min"
sample: src/multitenancy/Jaina.MultiTenancy/
---

# Multi-tenant SaaS: separate tenants without separate databases

## The Story

Tuesday afternoon, two months after launch. You ship a quick fix to the orders endpoint. At 14:31 the support inbox lights up: an Acme Corp ops engineer is reporting that they can see orders from Globex Industries in their dashboard. By 14:33 you're rolling back the deploy and screenshotting the breach for the post-mortem. The bug? You forgot a single `WHERE tenant_id = @tenantId` clause when you refactored the query.

There are three ways to keep tenants apart:

1. **Database per tenant** — bullet-proof, expensive, painful at 1,000+ tenants
2. **Schema per tenant** — moderate, painful migrations
3. **Shared schema with row-level isolation** — cheap, fast, **one missing predicate = catastrophe**

Most SaaS apps land on (3) for cost reasons. The catch is making it impossible to forget the predicate. That's what `Jaina.MultiTenancy` does.

## Naive approach

```csharp
public async Task<Order[]> ListAsync(string tenantId, CancellationToken ct) =>
    await _db.Orders.Where(o => o.TenantId == tenantId).ToArrayAsync(ct);
```

What breaks:

- A new endpoint someone added doesn't filter — leak.
- A LINQ subquery in a different layer doesn't filter — leak.
- A reporting job runs as "system" without a tenant — sees everything, may dump it.
- A junior dev refactors and "forgets" — leak. (This is the most common.)

Code review catches some. None of them catch all.

## Jaina solution

Two layers: **resolve the tenant up front** (middleware), **enforce the filter at the data layer** (EF query filter).

### Resolve the tenant

```csharp
// Program.cs
builder.Services.AddJainaMultiTenancy(b => b
    .FromHeader("X-Tenant")
    .FromClaim("tid")
    .FromHost(@"^([^.]+)\.api\.example\.com$"));

app.UseAuthentication();
app.UseAuthorization();
app.UseJainaTenantResolution();   // populates ITenantContext for the request scope
```

The four built-in resolvers (header / claim / host / route) chain in priority order — first non-null wins. Source: [`Resolvers.cs`](../../src/multitenancy/Jaina.MultiTenancy/Resolvers.cs).

### Inject and use

```csharp
public class OrdersService(ITenantContext tenants, AppDb db)
{
    public async Task<Order[]> ListAsync(CancellationToken ct)
    {
        if (!tenants.HasTenant)
            return Array.Empty<Order>();   // anonymous traffic — return nothing

        // No need to .Where(o => o.TenantId == ...) — the EF filter does it
        return await db.Orders.ToArrayAsync(ct);
    }
}
```

### EF Core query filter (lands in Jaina.MultiTenancy.EfCore — preview)

```csharp
public class AppDb : DbContext
{
    private readonly ITenantContext _tenants;
    public AppDb(DbContextOptions<AppDb> options, ITenantContext tenants) : base(options)
        => _tenants = tenants;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // One line — every Order query auto-scoped to the current tenant
        modelBuilder.Entity<Order>().HasQueryFilter(o => o.TenantId == _tenants.Current!.TenantId);
    }
}
```

Now the only way to bypass the filter is `IgnoreQueryFilters()` — visible at the call site, easy to grep for.

## Happy path

```bash
# Acme — header sets the tenant
$ curl -H "X-Tenant: acme" http://localhost:5000/api/orders
[
  {"id": "o1", "sku": "WIDGET", "tenantId": "acme"},
  {"id": "o2", "sku": "GADGET", "tenantId": "acme"}
]

# Globex — different header, only their orders
$ curl -H "X-Tenant: globex" http://localhost:5000/api/orders
[
  {"id": "o3", "sku": "DEVICE", "tenantId": "globex"}
]
```

Both queries hit the same SQL: `SELECT * FROM Orders WHERE TenantId = @p0`. The handler doesn't write the predicate; the model does.

## Error scenarios

### 1. Cross-tenant data leak attempted via a refactor

A new endpoint:

```csharp
app.MapGet("/api/orders/{id:guid}", async (Guid id, AppDb db) =>
    await db.Orders.FirstOrDefaultAsync(o => o.Id == id));
```

The author forgot to filter by tenant. With the EF query filter, the generated SQL is still:

```sql
SELECT * FROM Orders WHERE Id = @id AND TenantId = @tenantId
```

Acme's endpoint asking for an order id that belongs to Globex returns null — same shape as "not found". The leak is impossible without explicitly bypassing the filter.

### 2. Tenant header missing

```bash
$ curl http://localhost:5000/api/orders
[]
```

`ITenantContext.HasTenant == false`. The handler short-circuits to empty. You can also configure stricter behaviour: have a middleware return 400 if the request hits a tenant-scoped endpoint without a resolved tenant.

### 3. JWT claim resolution overrides the header

When a user is authenticated, the `tid` claim trumps the `X-Tenant` header (because `FromClaim("tid")` was registered after `FromHeader` in the example, and the composite walks resolvers in registration order until one returns non-null — first non-null wins). This prevents a logged-in Acme user from spoofing the header to peek at Globex.

```bash
$ curl -H "Authorization: Bearer <Acme JWT>" -H "X-Tenant: globex" /api/orders
# Returns Acme orders — claim wins
```

### 4. Subdomain routing for branded tenant URLs

```bash
$ curl https://acme.api.example.com/orders   # no header needed
```

`HostTenantResolver` extracts `acme` from the host via the configured regex. Branded URLs work without any client-side wiring.

### 5. Per-tenant connection-string isolation (preview)

For tenants that need their own database (regulatory, very large), `ITenantStore` plus a tenant-aware `DbContextFactory` selects the right connection string at runtime:

```csharp
public class TenantAwareDbContextFactory(ITenantContext tenants, ITenantStore store)
{
    public AppDb Create()
    {
        var connection = store.GetConnectionString(tenants.Current!.TenantId);
        var options = new DbContextOptionsBuilder<AppDb>().UseNpgsql(connection).Options;
        return new AppDb(options, tenants);
    }
}
```

Same code, different DB. Lands in `Jaina.MultiTenancy.EfCore` shortly.

### 6. Background job runs without an HTTP request

A Quartz job has no `HttpContext`. You explicitly set the tenant on the job:

```csharp
public sealed class NightlyDigestJob(ITenantContext tenants, AppDb db) : IBackgroundJob<NightlyArgs>
{
    public async Task ExecuteAsync(NightlyArgs args, CancellationToken ct)
    {
        // job runs once per tenant — args.TenantId is set by the scheduler
        ((TenantContext)tenants).Set(new TenantInfo { TenantId = args.TenantId });
        var todays = await db.Orders.Where(o => o.PlacedAt > DateTime.UtcNow.AddDays(-1)).ToArrayAsync(ct);
        // ... build digest
    }
}
```

The query filter still applies because the saga set the tenant before touching the DbContext.

## What you'd see in production

OTEL trace tags (auto-emitted via `JainaActivitySource` + `TagConventions`):

```
GET /api/orders             span: api.orders.list
  jaina.tenant.id = "acme"
  jaina.user.id   = "user_42"
  jaina.correlation.id = "abc-123"
  ↓
  db.query                  span: ef.orders.list
    db.statement = "SELECT * FROM Orders WHERE TenantId = @p0"
```

Use `jaina.tenant.id` as a Loki/Tempo dimension to slice every metric by tenant — top-N tenants by latency, tenants generating the most errors, the noisiest neighbour in shared infra.

Useful metrics:

- `jaina.tenant.requests` counter by tenant — find the noisy neighbours
- `jaina.tenant.errors` counter by tenant — alert on per-tenant error rate spikes
- Per-tenant SLI dashboards by partitioning your existing latency histogram on `jaina.tenant.id`

## Trade-offs & gotchas

- **The `IgnoreQueryFilters()` escape hatch is real.** It's necessary for admin tooling, reporting, support impersonation. Build a static analyzer rule (or grep in code review) that flags every use site.
- **Per-tenant `IOptionsSnapshot` doesn't compose with the default cache.** The framework's `TenantOptionsCache<TOptions>` partitions the per-tenant options correctly; if you wire your own options pattern, mirror that approach.
- **Cache keys must include the tenant.** The Redis cache `Get("user-42")` will return Acme's user 42 to Globex if the key isn't tenant-scoped. `Jaina.Caching` providers will get a per-tenant key prefix in a follow-up; today, scope the keys yourself.
- **Background jobs and message handlers must explicitly set the tenant.** They don't have an HTTP request to resolve from. Wire it in the job/saga payload.
- **Cross-tenant operations are a separate concern.** Building an "all-tenants-of-this-billing-account" view requires bypassing the filter — make these endpoints conspicuous (`/api/admin/...`), authenticated to a separate scope, and audited.

## Try it yourself

The resolvers are unit-tested deterministically (no need for a real HTTP server):

```bash
dotnet test tests/unit/Jaina.MultiTenancy.UnitTests/Jaina.MultiTenancy.Tests.csproj -f net8.0
```

End-to-end exercise (in your own app):

```bash
# Header
curl -H "X-Tenant: acme" http://localhost:5000/api/orders

# Subdomain (after configuring the resolver regex + DNS)
curl https://acme.api.example.com/orders

# Claim (after JWT auth — tid claim wins over header)
curl -H "Authorization: Bearer ..." -H "X-Tenant: globex" http://localhost:5000/api/orders
```

## Further reading

- Source: [`Resolvers.cs`](../../src/multitenancy/Jaina.MultiTenancy/Resolvers.cs), [`TenantResolutionMiddleware.cs`](../../src/multitenancy/Jaina.MultiTenancy/TenantResolutionMiddleware.cs), [`ITenantContext.cs`](../../src/multitenancy/Jaina.MultiTenancy/ITenantContext.cs)
- Tests (10/10 — each resolver hit + miss + composite first-non-null wins): [`TenantResolverTests.cs`](../../tests/unit/Jaina.MultiTenancy.UnitTests/TenantResolverTests.cs)
- Companion posts: [Idempotency](2026-05-04-idempotency-retry-storm.md) (per-tenant idempotency keys), [Outbox](2026-05-04-outbox-black-friday.md) (carry tenant in message headers)
