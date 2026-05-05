---
title: "gRPC + JWT + tenant: auth flow between microservices"
date: 2026-05-05
tags: [grpc, auth, multitenancy, jwt]
reading_time: "~7 min"
sample: src/grpc/Jaina.Grpc/
---

# gRPC + JWT + tenant: auth flow between microservices

## The Story

Tuesday 11:47 AM. Orders calls Inventory via gRPC. Audit log shows Acme tenant's user reserved Globex tenant's stock. Two-tenant data leak through the back door — the gRPC channel between services. The Authorization header was being passed through but the `tid` claim was being ignored on the receiving side.

This is the back door most teams forget: REST through the gateway is hardened, but **internal RPC channels are anonymous-by-default**.

## The shape

`Orders` (web service) → calls `Inventory.Reserve(req)` (gRPC service):

1. User calls `POST /orders` with `Authorization: Bearer <jwt>` (carries `tid=acme`)
2. Orders service validates the JWT, sees `tid=acme`
3. Orders calls Inventory via gRPC — **must propagate the JWT** so Inventory can re-validate and read the same `tid`
4. Inventory's authorization policy gates the operation by tenant scope

If you skip step 3, Inventory has no idea who the caller is. Worst case: it trusts the inputs blindly.

## Jaina solution

```csharp
// Inventory.csproj — gRPC server
builder.Services.AddJainaUserContext();           // IUserContext from HttpContext
builder.Services.AddJainaMultiTenancy(b => b.FromClaim("tid"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(/* same secret as Identity service */);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("orders.write", p => p.RequireJainaScope("orders.write"));

builder.Services.AddGrpc();

app.UseAuthentication();
app.UseAuthorization();
app.UseJainaTenantResolution();   // populates ITenantContext from the validated tid claim

app.MapGrpcService<InventoryService>();
```

Service:

```csharp
public class InventoryService(ITenantContext tenants, IUserContext users) : Inventory.InventoryBase
{
    [Authorize(Policy = "orders.write")]
    public override Task<ReserveReply> Reserve(ReserveRequest req, ServerCallContext ctx)
    {
        // tenants.Current.TenantId is what the JWT claimed — not what the request body said
        // users.UserId tells us which user inside that tenant initiated this
        return DoReserveAsync(req, tenants.Current!.TenantId, users.UserId);
    }
}
```

Caller:

```csharp
// Orders.csproj — gRPC client
public class OrdersHandler(GrpcChannel channel, IUserContext currentUser, IHttpContextAccessor accessor)
{
    public async Task PlaceAsync(...)
    {
        var client = new Inventory.InventoryClient(channel);
        var headers = new Metadata();
        // Forward the bearer token verbatim — Inventory will re-validate.
        var token = accessor.HttpContext!.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(token))
            headers.Add("Authorization", token);

        await client.ReserveAsync(req, headers);
    }
}
```

`Jaina.Grpc` ships interceptors that handle the boilerplate (logging + correlation id propagation today; auth-token forwarding lands as `JainaAuthClientInterceptor` shortly). Source: [`src/grpc/Jaina.Grpc/`](../../src/grpc/Jaina.Grpc/).

## Happy path

```
[orders]   POST /api/orders { sku, qty } with Authorization: Bearer eyJ...
[orders]   JWT validated; ITenantContext = "acme", UserId = "user_42", scope = "orders.write"
[orders]   gRPC -> Inventory.Reserve, Authorization metadata propagated
[inventory] JWT re-validated; same tid, same scope; AuthorizationFilter passes
[inventory] returns ReserveReply
[orders]   commits order; 201 to caller
```

Both sides agree on `tid=acme`. No way to slip through without it.

## Error scenarios

### 1. Token expired between hops

The JWT has `exp = 11:47:00`. Orders calls Inventory at 11:47:01. Inventory rejects with `401`. Orders returns 401 to the user. Mitigation: refresh tokens proactively if the remaining lifetime is below a threshold; or use a longer-lived service token for service-to-service and a short user token for the original request.

### 2. Caller forgot to propagate

Inventory sees no Authorization header → 401. Easy to spot in logs (`audience missing`). Fix: enforce in the client interceptor — fail fast in the caller if the user is authenticated but no token is being forwarded.

### 3. mTLS-only path

For high-trust internal calls you may want to drop the JWT and rely on mutual TLS. Configure both ends with `AddCertificateAuthentication`; map the SAN to a service identity claim and write your scope policy against that instead. (mTLS helpers live in `Jaina.Security.Authentication.mTls` — preview, lands in 1.1.)

### 4. Tenant header spoofing on the gRPC side

The gRPC server doesn't read the `X-Tenant` header — `FromClaim("tid")` only trusts the JWT signature. A caller forging `X-Tenant: globex` in metadata is ignored. (If you also enable `FromHeader`, you'll create the back door — don't.)

### 5. Different scope per environment

Production scopes are stricter. Use `AddJsonOptions` to bind policy → scope mapping from configuration so prod / staging diverge without redeploys.

### 6. Streaming RPC and token expiry mid-stream

A 5-minute server-streaming call started at 11:46 with a token expiring at 11:50. The server must validate **on every message** if you care about expiry, or accept the trade-off ("if validated at start, trust for stream lifetime"). The Auth interceptor handles this depending on configuration.

## What you'd see in production

OTEL trace across services:

```
POST /api/orders               jaina.orders.place                250ms
  user.id     = "user_42"
  tenant.id   = "acme"
  ├─ rpc.client.duration       grpc Inventory.Reserve            120ms
  │  ├─ rpc.system  = "grpc"
  │  ├─ rpc.method  = "Reserve"
  │  └─ → server span (linked):
  │      Inventory.Reserve     jaina.inventory.reserve            110ms
  │        user.id  = "user_42"
  │        tenant.id= "acme"
  │        scope    = "orders.write"
```

Both spans agree on `tenant.id`. If they ever disagree → bug.

## Trade-offs & gotchas

- **JWT validation is not free.** Cache the public key (JWKS) and re-fetch on rotation; both `JwtBearerHandler` and the gRPC variant do this by default — but if you proxy through a layer that strips it, you'll re-pay.
- **Service-to-service can use long-lived tokens or mTLS** to avoid expiry mid-saga; user tokens stay short.
- **Don't trust gRPC metadata other than `Authorization`** for identity. The interceptor that reads claims must come from a validated source (`HttpContext.User` after `UseAuthentication`).
- **Audit by `tid`**, not by `user`. Most leak hunts care which tenant saw what; user-level audit is for compliance but not the first thing you grep.

## Try it yourself

The `JainaShop.AppHost` sample currently wires REST gateway → REST orders. The gRPC variant ships as a follow-up commit (`samples/JainaShop/JainaShop.Inventory` gRPC service + interceptors). Today, exercise the JWT + tenant flow at the REST layer:

```bash
TOKEN=$(curl -s -X POST http://localhost:5103/tokens \
        -H "Content-Type: application/json" \
        -d '{"username":"alice@acme","password":"alice123"}' | jq -r .access_token)

curl -X POST http://localhost:5000/api/orders \
     -H "Authorization: Bearer $TOKEN" \
     -H "Idempotency-Key: demo-1" \
     -H "Content-Type: application/json" \
     -d '{"sku":"WIDGET","quantity":1,"unitPrice":9.99}'
```

Same flow, REST instead of gRPC. The JWT carries `tid=acme`; the gateway and downstream services both honour it.

## Further reading

- Source: [`Jaina.Security.Authentication`](../../src/security/Jaina.Security.Authentication/), [`Jaina.Grpc`](../../src/grpc/Jaina.Grpc/)
- [JWT Bearer in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/jwtauthn)
- Companion: [Multi-tenancy](2026-05-05-multi-tenancy.md)
