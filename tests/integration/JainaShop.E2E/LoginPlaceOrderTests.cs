using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting.Testing;

namespace JainaShop.E2E;

/// <summary>
/// End-to-end smoke covering the JainaShop sample:
///   login (Identity) → place order (Gateway → Orders) → outbox dispatch
///   + idempotency replay + tenant guard.
/// </summary>
public sealed class LoginPlaceOrderTests : IClassFixture<AppHostFixture>
{
    private readonly AppHostFixture _fx;

    public LoginPlaceOrderTests(AppHostFixture fx) => _fx = fx;

    [Fact]
    public async Task Login_PlaceOrder_OutboxDispatch_AndIdempotencyReplay()
    {
        // Arrange
        var identity = _fx.App.CreateHttpClient("identity");
        var orders   = _fx.App.CreateHttpClient("orders");
        var gateway  = _fx.App.CreateHttpClient("gateway");

        // Act 1: login
        var loginResp = await identity.PostAsJsonAsync("/tokens",
            new { Username = "alice@acme", Password = "alice123" });

        // Assert 1: JWT issued + tid claim resolves tenant
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var loginBody = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = loginBody.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(jwt));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        Assert.Equal("acme", token.Claims.Single(c => c.Type == "tid").Value);
        Assert.Contains("orders.write", token.Claims.Single(c => c.Type == "scope").Value);

        // Act 2: place order via Gateway with tenant + idempotency key
        var idemKey = Guid.NewGuid().ToString();
        var placeReq = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { Sku = "WIDGET-001", Quantity = 2, UnitPrice = 9.99m }),
        };
        placeReq.Headers.Add("X-Tenant", "acme");
        placeReq.Headers.Add("Idempotency-Key", idemKey);
        placeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var placeResp = await gateway.SendAsync(placeReq);
        Assert.Equal(HttpStatusCode.OK, placeResp.StatusCode);

        var placed = await placeResp.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = placed.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, orderId);
        Assert.Equal("WIDGET-001", placed.GetProperty("sku").GetString());
        Assert.Equal(19.98m, placed.GetProperty("total").GetDecimal());

        // Act 3: read back from Orders direct
        var getResp = await orders.GetAsync($"/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var fetched = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderId, fetched.GetProperty("id").GetGuid());

        // Act 4: outbox enqueued + dispatched (relay polls every 500ms — give it a moment)
        JsonElement outboxArr = default;
        var outboxOk = false;
        for (var i = 0; i < 20 && !outboxOk; i++)
        {
            var outboxResp = await orders.GetAsync("/_outbox");
            outboxArr = await outboxResp.Content.ReadFromJsonAsync<JsonElement>();
            outboxOk = outboxArr.EnumerateArray().Any(m =>
                m.GetProperty("payloadType").GetString() == "JainaShop.Orders.OrderPlaced" &&
                m.GetProperty("destination").GetString() == "orders.events");
            if (!outboxOk) await Task.Delay(250);
        }
        Assert.True(outboxOk, "OrderPlaced never appeared in /_outbox");

        // Act 5: idempotency replay — same key, same response
        var replayReq = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { Sku = "WIDGET-001", Quantity = 2, UnitPrice = 9.99m }),
        };
        replayReq.Headers.Add("X-Tenant", "acme");
        replayReq.Headers.Add("Idempotency-Key", idemKey);
        replayReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var replayResp = await gateway.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.OK, replayResp.StatusCode);
        var replayed = await replayResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderId, replayed.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Gateway_RejectsRequestWithoutTenantHeader()
    {
        var gateway = _fx.App.CreateHttpClient("gateway");

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { Sku = "WIDGET-001", Quantity = 1, UnitPrice = 9.99m }),
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var resp = await gateway.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Login_BadPassword_Returns401()
    {
        var identity = _fx.App.CreateHttpClient("identity");

        var resp = await identity.PostAsJsonAsync("/tokens",
            new { Username = "alice@acme", Password = "WRONG" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
