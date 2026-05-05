using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Security.Authentication.ApiKey;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add the <c>ApiKey</c> authentication scheme. Configure at minimum either
    /// <c>StaticKeys</c> (testing / dev) or <c>KeyResolver</c> (production lookup).
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
    ///         .AddJainaApiKey(o => o.StaticKeys["dev-key"] = "ci-bot");
    /// </code>
    /// </example>
    public static AuthenticationBuilder AddJainaApiKey(
        this AuthenticationBuilder builder,
        Action<ApiKeyAuthenticationOptions>? configure = null) =>
        builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme,
            configure ?? (_ => { }));
}
