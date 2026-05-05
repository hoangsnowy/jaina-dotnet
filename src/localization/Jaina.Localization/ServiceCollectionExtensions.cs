using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Localization;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register <c>IStringLocalizer</c> with the conventional resources path
    /// (<c>Resources</c> folder relative to the project root). Apps that need a different
    /// path call <c>services.AddLocalization(o => o.ResourcesPath = "...")</c> directly.
    /// </summary>
    public static IServiceCollection AddJainaLocalization(this IServiceCollection services, string resourcesPath = "Resources")
    {
        services.AddLocalization(o => o.ResourcesPath = resourcesPath);
        return services;
    }
}
