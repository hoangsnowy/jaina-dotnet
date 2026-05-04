using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Security.Authentication.UserContext;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="IUserContext"/> as a scoped service backed by
    /// <c>HttpContextAccessor</c>. Domain code injects <see cref="IUserContext"/> instead
    /// of taking an <c>HttpContext</c> dependency.
    /// </summary>
    public static IServiceCollection AddJainaUserContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<IUserContext, HttpUserContext>();
        return services;
    }
}
