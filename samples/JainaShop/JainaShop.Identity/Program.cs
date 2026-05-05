using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Jaina.AspNetCore;
using Jaina.HealthChecks;
using Jaina.Samples.ServiceDefaults;
using Jaina.Security.Authentication.ApiKey;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddJainaProblemDetails();

const string secret = "demo-secret-do-not-use-in-production-32+chars";
const string issuer = "https://jainashop.local/identity";
const string audience = "jainashop";

builder.Services.AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
    .AddJainaApiKey(o =>
    {
        o.StaticKeys["dev-key-orders"]    = "service:orders";
        o.StaticKeys["dev-key-shipping"]  = "service:shipping";
    });

builder.Services.AddAuthorization();

builder.Services.AddHealthChecks()
    .AddCheck("identity-ready", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
        tags: new[] { JainaHealthCheckTags.Ready });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseJainaPipeline();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapJainaHealthChecks();

// Hardcoded users — demo only. Production: hash + DB lookup.
var users = new Dictionary<string, (string Password, string[] Scopes)>
{
    ["alice@acme"]    = ("alice123",  new[] { "orders.read", "orders.write" }),
    ["bob@globex"]    = ("bob123",    new[] { "orders.read" }),
};

// POST /tokens — exchange username+password for a JWT bearer token
app.MapPost("/tokens", (LoginRequest req) =>
{
    if (!users.TryGetValue(req.Username, out var u) || u.Password != req.Password)
        return Results.Problem(statusCode: 401, title: "Unauthorized", detail: "Bad username or password");

    var tenant = req.Username.Contains('@') ? req.Username[(req.Username.IndexOf('@') + 1)..] : "default";
    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, req.Username),
        new(JwtRegisteredClaimNames.Email, req.Username),
        new("tid", tenant),
        new("scope", string.Join(' ', u.Scopes)),
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        notBefore: DateTime.UtcNow,
        expires: DateTime.UtcNow.AddMinutes(30),
        signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

    return Results.Ok(new
    {
        access_token = new JwtSecurityTokenHandler().WriteToken(token),
        token_type = "Bearer",
        expires_in = 1800,
    });
});

// GET /me — protected by API key scheme; returns the resolved owner
app.MapGet("/me", (HttpContext ctx) =>
    Results.Ok(new
    {
        method = ctx.User.FindFirst("auth_method")?.Value ?? "anonymous",
        sub    = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
              ?? ctx.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
        scopes = ctx.User.FindFirst("scope")?.Value,
    })
).RequireAuthorization();

app.Run();

public record LoginRequest(string Username, string Password);
