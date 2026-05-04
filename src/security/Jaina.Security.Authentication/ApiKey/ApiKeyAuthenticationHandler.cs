using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jaina.Security.Authentication.ApiKey;

/// <summary>
/// Authenticate requests by a static API key in a configurable header. Useful for
/// service-to-service calls and trusted internal automation. Issues a
/// <see cref="ClaimsPrincipal"/> with <c>NameIdentifier = owner</c> and a fixed
/// <c>auth_method = api-key</c> claim so policies can distinguish key auth from JWT.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var values))
            return AuthenticateResult.NoResult();

        var key = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
            return AuthenticateResult.NoResult();

        string? owner;
        if (Options.KeyResolver is not null)
            owner = await Options.KeyResolver(key, Context.RequestAborted);
        else
            owner = Options.StaticKeys.TryGetValue(key, out var staticOwner) ? staticOwner : null;

        if (string.IsNullOrEmpty(owner))
            return AuthenticateResult.Fail("Invalid API key");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, owner),
            new Claim(ClaimTypes.Name, owner),
            new Claim("auth_method", "api-key"),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
