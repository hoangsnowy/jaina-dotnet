using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Jaina.Security.Authentication;

public class JainaAuthenticationOptions
{
    public string Authority { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaJwtAuthentication(this IServiceCollection services, Action<JainaAuthenticationOptions> configure)
    {
        var options = new JainaAuthenticationOptions();
        configure(options);

        services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(o =>
        {
            o.Authority = options.Authority;
            o.Audience = options.Audience;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = options.ValidateIssuer,
                ValidateAudience = options.ValidateAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = !string.IsNullOrEmpty(options.SecretKey),
                IssuerSigningKey = string.IsNullOrEmpty(options.SecretKey)
                    ? null
                    : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey)),
            };
        });

        services.AddAuthorization();
        return services;
    }
}
