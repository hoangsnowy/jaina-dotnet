using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Validation;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Scan the given assemblies for <see cref="IValidator{T}"/> implementations and register
    /// them as scoped services. Pair with <c>endpoint.AddJainaValidation()</c> so the filter
    /// can resolve and execute them.
    /// </summary>
    public static IServiceCollection AddJainaValidation(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
            assemblies = new[] { Assembly.GetCallingAssembly() };
        services.AddValidatorsFromAssemblies(assemblies);
        return services;
    }
}
